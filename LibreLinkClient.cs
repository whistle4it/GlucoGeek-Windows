using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Librewin;

internal sealed class SessionExpired : Exception { }
internal sealed class LibreLinkClient : IGlucoseClient
{
    private readonly HttpClient http;
    private readonly Settings settings;
    private readonly Func<string> passwordProvider;
    private string host = "https://api.libreview.io", token = "", account = "";
    private DateTimeOffset expires;
    public LibreLinkClient(Settings settings, HttpMessageHandler? handler = null, Func<string>? passwordProvider = null)
    {
        this.settings = settings;
        this.passwordProvider = passwordProvider ?? (() => settings.Password);
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
    }
    public static bool SafeHost(string value) => Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == "https" && u.IsDefaultPort && u.UserInfo == "" && u.Query == "" && u.Fragment == "" && u.AbsolutePath == "/" && (u.Host == "libreview.io" || u.Host.EndsWith(".libreview.io", StringComparison.OrdinalIgnoreCase));
    private async Task<JsonElement> Send(string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, host.TrimEnd('/') + path);
        request.Headers.TryAddWithoutValidation("product", settings.ApiProduct);
        request.Headers.TryAddWithoutValidation("version", settings.ApiVersion);
        request.Headers.TryAddWithoutValidation("User-Agent", settings.UserAgent);
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        request.Headers.TryAddWithoutValidation("cache-control", "no-cache");
        if (account.Length > 0) request.Headers.TryAddWithoutValidation("account-id", account);
        if (token.Length > 0) request.Headers.TryAddWithoutValidation("authorization", "Bearer " + token);
        if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && body == null) throw new SessionExpired();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"LibreLinkUp HTTP {(int)response.StatusCode}." + (response.StatusCode == HttpStatusCode.Forbidden ? " The service rejected this client. Check the API version in Settings." : " Please retry later."));
        // Do not log response bodies: they can contain tokens and personal data.
        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var status = root.Get("status").Number();
        if (status == 2 && body != null) throw new InvalidOperationException("Sign-in rejected. Use your LibreLinkUp follower account, not LibreLink.");
        if (status == 4) throw new InvalidOperationException("Open the official LibreLinkUp app and complete the required account steps, then retry.");
        if (status is not null && status != 0) throw new InvalidOperationException($"LibreLinkUp returned status {status}. Open its official app to check your account.");
        var data = root.Get("data");
        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) throw new InvalidDataException("LibreLinkUp returned an incomplete response.");
        return data.Clone();
    }
    private async Task Login(CancellationToken ct)
    {
        token = account = "";
        if (string.IsNullOrWhiteSpace(settings.Email) || settings.PasswordProtected.Length == 0) throw new InvalidOperationException("Open Settings to connect your LibreLinkUp follower account.");
        for (int i = 0; i < 3; i++)
        {
            var data = await Send("/llu/auth/login", new { email = settings.Email, password = passwordProvider() }, ct);
            if (data.Get("redirect").ValueKind == JsonValueKind.True)
            {
                var region = data.Get("region").Text().ToLowerInvariant();
                var config = await Send("/llu/config/country?country=DE", null, ct);
                var next = config.Get("regionalMap").Get(region).Get("lslApi").Text();
                if (!SafeHost(next)) throw new InvalidDataException("LibreLinkUp returned an unrecognized regional endpoint.");
                host = next.TrimEnd('/');
                continue;
            }
            var ticket = data.Get("authTicket");
            token = ticket.Get("token").Text();
            var user = data.Get("user").Get("id").Text();
            if (token.Length == 0 || user.Length == 0) throw new InvalidDataException("Sign-in returned no valid session. Check your account in LibreLinkUp.");
            account = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user))).ToLowerInvariant();
            var seconds = ticket.Get("expires").Number();
            expires = seconds is > 0 and < 253402300799 ? DateTimeOffset.FromUnixTimeSeconds((long)seconds.Value) : DateTimeOffset.UtcNow.AddHours(1);
            return;
        }
        throw new InvalidDataException("LibreLinkUp exceeded the regional redirect limit.");
    }
    public async Task<Snapshot> Fetch(CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                if (token.Length == 0 || expires < DateTimeOffset.UtcNow.AddMinutes(1)) await Login(ct);
                var connections = (await Send("/llu/connections", null, ct)).Items().ToArray();
                var selected = Choose(connections, settings.Patient);
                var id = selected.Get("patientId").Text();
                if (id.Length == 0) throw new InvalidDataException("The selected connection has no patient identifier.");
                var graph = await Send("/llu/connections/" + Uri.EscapeDataString(id) + "/graph", null, ct);
                if (graph.Get("connection").Get("patientId").Text() != id) throw new InvalidDataException("The graph did not match the selected patient.");
                return Snapshot.Parse(graph);
            }
            catch (SessionExpired) when (attempt == 0) { token = account = ""; }
        }
        throw new InvalidOperationException("Session expired. Reconnect in Settings.");
    }
    internal static JsonElement Choose(JsonElement[] connections, string wanted)
    {
        if (connections.Length == 0) throw new InvalidOperationException("No followed patients. Accept an invitation in LibreLinkUp first.");
        if (string.IsNullOrWhiteSpace(wanted))
        {
            if (connections.Length == 1) return connections[0];
            throw new InvalidOperationException("You follow multiple people. Enter the exact patient name in Settings.");
        }
        var matches = connections.Where(c => string.Equals(Snapshot.Name(c), wanted.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Patient name must match exactly one followed person. Check Settings.");
        return matches[0];
    }
    public void Dispose() => http.Dispose();
}
