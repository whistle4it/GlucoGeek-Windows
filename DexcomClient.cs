using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace GlucoGeek;

// Independent Share protocol implementation; protocol reference: gagebenne/pydexcom.
// This is an unofficial cloud integration, not a direct sensor connection.
internal sealed class DexcomClient : IGlucoseClient
{
    private readonly Settings settings;
    private readonly HttpClient http;
    private readonly Func<string> password;
    private string session = "";
    private string? authFailure;
    internal static string Host(string region) => region switch
    {
        "US" => "https://share2.dexcom.com/ShareWebServices/Services/",
        "Outside US" => "https://shareous1.dexcom.com/ShareWebServices/Services/",
        "Japan" => "https://share.dexcom.jp/ShareWebServices/Services/",
        _ => throw new InvalidOperationException("Select your Dexcom account region in Settings.")
    };
    private string ApplicationId => settings.DexcomRegion == "Japan" ? "d8665ade-9673-4e27-9ff6-92db4ce13d13" : "d89443d2-327c-4a6f-89e5-496bbb0317db";
    public DexcomClient(Settings settings, HttpMessageHandler? handler = null, Func<string>? passwordProvider = null)
    {
        this.settings = settings;
        password = passwordProvider ?? (() => settings.DexcomPassword);
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
    }
    private async Task<JsonElement> Post(string path, object body, bool reading, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Host(settings.DexcomRegion) + path);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (HttpRequestException) { throw new InvalidOperationException("Cannot reach Dexcom Share. Check your connection and region."); }
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            JsonElement root;
            try { using var doc = JsonDocument.Parse(text); root = doc.RootElement.Clone(); }
            catch (JsonException) { throw new InvalidDataException("Dexcom Share returned an unreadable response. Please retry later."); }
            var code = root.Get("Code").Text();
            if (reading && (response.StatusCode == HttpStatusCode.Unauthorized || code is "SessionIdNotFound" or "SessionNotValid")) throw new SessionExpired();
            if (code is "AccountPasswordInvalid" or "SSO_AuthenticateMaxAttemptsExceeded" or "SSO_InternalError" || !reading && response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                authFailure = "Dexcom sign-in failed or is temporarily locked. Check the wearer's username, password and region, then Save & connect again in Settings.";
                throw new InvalidOperationException(authFailure);
            }
            // Never display response bodies or URLs: they can contain credentials/session IDs.
            if (!response.IsSuccessStatusCode || code.Length > 0) throw new InvalidOperationException($"Dexcom Share request failed (HTTP {(int)response.StatusCode}). Check Share in the G7 app and retry later.");
            return root;
        }
    }
    private static string Identifier(JsonElement value)
    {
        if (!Guid.TryParse(value.Text(), out var id) || id == Guid.Empty) throw new InvalidDataException("Dexcom returned an invalid account or session identifier.");
        return id.ToString();
    }
    private async Task Login(CancellationToken ct)
    {
        if (authFailure != null) throw new InvalidOperationException(authFailure);
        if (string.IsNullOrWhiteSpace(settings.DexcomUsername) || settings.DexcomPasswordProtected.Length == 0)
            throw new InvalidOperationException("Enter the G7 wearer's Dexcom username and password in Settings.");
        var secret = password();
        var account = Identifier(await Post("General/AuthenticatePublisherAccount", new { accountName = settings.DexcomUsername, password = secret, applicationId = ApplicationId }, false, ct));
        session = Identifier(await Post("General/LoginPublisherAccountById", new { accountId = account, password = secret, applicationId = ApplicationId }, false, ct));
    }
    public async Task<Snapshot> Fetch(CancellationToken ct)
    {
        if (!double.IsFinite(settings.DexcomLow) || !double.IsFinite(settings.DexcomHigh) || settings.DexcomLow <= 0 || settings.DexcomHigh <= settings.DexcomLow)
            throw new InvalidOperationException("Set valid Dexcom display bounds in Settings.");
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                if (session.Length == 0) await Login(ct);
                var data = await Post("Publisher/ReadPublisherLatestGlucoseValues?sessionId=" + Uri.EscapeDataString(session) + "&minutes=180&maxCount=36", new { }, true, ct);
                return Parse(data, settings);
            }
            catch (SessionExpired) when (attempt == 0) { session = ""; }
        }
        throw new InvalidOperationException("Dexcom session expired. Reconnect in Settings.");
    }
    internal static DateTimeOffset? Timestamp(string text)
    {
        var match = Regex.Match(text, @"^/?Date\((-?\d+)(?:[+-]\d{4})?\)/?$", RegexOptions.CultureInvariant);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)) return null;
        try { return DateTimeOffset.FromUnixTimeMilliseconds(ms); } catch (ArgumentOutOfRangeException) { return null; }
    }
    internal static int Trend(JsonElement value) => (value.ValueKind == JsonValueKind.Number ? value.Number()?.ToString(CultureInfo.InvariantCulture) : value.Text()) switch
    {
        "DoubleUp" or "1" => 7, "SingleUp" or "2" => 5, "FortyFiveUp" or "3" => 4,
        "Flat" or "4" => 3, "FortyFiveDown" or "5" => 2, "SingleDown" or "6" => 1, "DoubleDown" or "7" => 6, _ => 0
    };
    internal static Snapshot Parse(JsonElement data, Settings settings)
    {
        if (data.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Dexcom returned an unexpected readings response.");
        var readings = new List<Reading>();
        foreach (var item in data.EnumerateArray())
        {
            var at = Timestamp(item.Get("ST").Text()) ?? Timestamp(item.Get("DT").Text());
            var mg = item.Get("Value").Number();
            // Reject the entire response rather than presenting an older row as the newest reading.
            if (at == null || mg is null or <= 0) throw new InvalidDataException("Dexcom returned a reading with an invalid value or timestamp.");
            int trend = Trend(item.Get("Trend"));
            string description = trend switch { 5 => "Rising", 1 => "Falling", 7 => "Rising quickly", 6 => "Falling quickly", 4 => "Rising slightly", 2 => "Falling slightly", 3 => "Steady", _ => "Trend unavailable" };
            readings.Add(new(at.Value, mg.Value, trend, TrendDescription: description));
        }
        if (readings.Count == 0) throw new InvalidOperationException("No Dexcom readings in the last three hours. Enable Share in the G7 app and check its internet connection.");
        readings = readings.GroupBy(r => r.At).Select(g => g.First()).OrderBy(r => r.At).ToList();
        // Share does not return display units, target bounds, patient name, or sensor start time.
        return new(readings[^1], readings, "Dexcom G7", settings.DexcomLow, settings.DexcomHigh, false, null);
    }
    public void Dispose() => http.Dispose();
}
