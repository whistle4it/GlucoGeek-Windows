using System.Net;
using System.Text;
using System.Text.Json;
namespace GlucoGeek;

internal static class DexcomTests
{
    public static void Run(Action<bool, string> check)
    {
        JsonElement Json(string s) { using var d = JsonDocument.Parse(s); return d.RootElement.Clone(); }
        var old = JsonSerializer.Deserialize<Settings>("{\"Email\":\"old@example.invalid\",\"PasswordProtected\":\"saved\"}")!;
        check(old.Provider == "LibreLinkUp" && old.HasCredentials && old.DexcomPasswordProtected == "", "Existing LibreLinkUp settings remain compatible");
        old.Provider = "Dexcom G7";
        check(!old.HasCredentials, "Dexcom cannot reuse LibreLinkUp credentials");
        check(DexcomClient.Timestamp("/Date(1700000000000+0100)/") == DateTimeOffset.FromUnixTimeMilliseconds(1700000000000), "Dexcom timestamp offset is not double-applied");
        check(DexcomClient.Timestamp("Date(1700000000000-0800)") == DateTimeOffset.FromUnixTimeMilliseconds(1700000000000), "Dexcom negative-offset date");
        check(DexcomClient.Timestamp("Date(9999999999999999999999)") == null, "Dexcom overflowing timestamp rejected");
        string[] trends = ["None", "DoubleUp", "SingleUp", "FortyFiveUp", "Flat", "FortyFiveDown", "SingleDown", "DoubleDown", "NotComputable", "RateOutOfRange"];
        int[] expected = [0, 7, 5, 4, 3, 2, 1, 6, 0, 0];
        for (int i = 0; i < trends.Length; i++)
            check(DexcomClient.Trend(Json(JsonSerializer.Serialize(trends[i]))) == expected[i] && DexcomClient.Trend(Json(i.ToString())) == expected[i], "Dexcom trend " + trends[i]);
        check(new Reading(DateTimeOffset.UtcNow, 100, 7).Arrow == "↑↑" && new Reading(DateTimeOffset.UtcNow, 100, 6).Arrow == "↓↓", "Rapid trends preserve double arrows");
        var settings = new Settings { Provider = "Dexcom G7", DexcomUsername = "dexcom-user", DexcomPasswordProtected = "test-only", DexcomRegion = "Outside US", DexcomLow = 75, DexcomHigh = 165 };
        foreach (var region in new[] { "US", "Outside US", "Japan" })
        {
            settings.DexcomRegion = region;
            using var handler = new Handler(region);
            using var client = new DexcomClient(settings, handler, () => "dexcom-secret");
            var snapshot = client.Fetch(CancellationToken.None).GetAwaiter().GetResult();
            check(snapshot.Current.Mgdl == 120 && snapshot.Current.Trend == 7 && snapshot.History.Count == 2 && snapshot.Previous?.Mgdl == 100, region + " auth and sorted/deduplicated history");
            check(snapshot.Low == 75 && snapshot.High == 165 && !snapshot.Mmol && snapshot.Sensor == null, region + " explicit display bounds, mg/dL and unknown sensor age");
            handler.Expire = 1;
            client.Fetch(CancellationToken.None).GetAwaiter().GetResult();
            check(handler.Logins == 2, region + " expired session renewed once");
            handler.Expire = 2;
            bool stopped = false; try { client.Fetch(CancellationToken.None).GetAwaiter().GetResult(); } catch (SessionExpired) { stopped = true; }
            check(stopped && handler.Logins == 3, region + " repeated expiry bounded");
        }
        bool failed = false;
        try { DexcomClient.Parse(Json("[]"), settings); } catch (InvalidOperationException) { failed = true; }
        check(failed, "Empty Dexcom response is not a live zero reading");
        failed = false;
        try { DexcomClient.Parse(Json("[{\"Value\":120,\"DT\":\"bad\"}]"), settings); } catch (InvalidDataException) { failed = true; }
        check(failed, "Malformed Dexcom reading rejected");
        using var badHandler = new Handler("Japan") { BadPassword = true };
        using var badClient = new DexcomClient(settings, badHandler, () => "dexcom-secret");
        for (int i = 0; i < 2; i++)
        {
            failed = false;
            try { badClient.Fetch(CancellationToken.None).GetAwaiter().GetResult(); }
            catch (InvalidOperationException ex) { failed = !ex.Message.Contains("dexcom-secret") && !ex.Message.Contains("PRIVATE"); }
            check(failed, "Authentication errors redact service payloads");
        }
        check(badHandler.Requests == 1, "Invalid credentials halt automatic authentication retries");
        using var cancelHandler = new Handler("Japan");
        using var cancelClient = new DexcomClient(settings, cancelHandler, () => "dexcom-secret");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        failed = false; try { cancelClient.Fetch(cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { failed = true; }
        check(failed, "Dexcom request cancellation");
    }
    private sealed class Handler(string region) : HttpMessageHandler
    {
        public int Requests, Logins, Expire;
        public bool BadPassword;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Requests++;
            if (request.Method != HttpMethod.Post || request.RequestUri!.Host != new Uri(DexcomClient.Host(region)).Host) throw new Exception("Wrong Dexcom method or host");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var path = request.RequestUri.AbsolutePath;
            string result;
            var status = HttpStatusCode.OK;
            if (path.Contains("General/"))
            {
                string application = region == "Japan" ? "d8665ade-9673-4e27-9ff6-92db4ce13d13" : "d89443d2-327c-4a6f-89e5-496bbb0317db";
                if (body.RootElement.Get("password").Text() != "dexcom-secret" || body.RootElement.Get("applicationId").Text() != application) throw new Exception("Wrong credentials or application ID");
                if (BadPassword) { result = "{\"Code\":\"AccountPasswordInvalid\",\"Message\":\"PRIVATE dexcom-secret\"}"; status = HttpStatusCode.InternalServerError; }
                else if (path.EndsWith("AuthenticatePublisherAccount"))
                {
                    if (body.RootElement.Get("accountName").Text() != "dexcom-user") throw new Exception("Wrong username");
                    result = "\"11111111-1111-1111-1111-111111111111\"";
                }
                else
                {
                    if (body.RootElement.Get("accountId").Text() != "11111111-1111-1111-1111-111111111111") throw new Exception("Wrong account ID");
                    Logins++; result = "\"22222222-2222-2222-2222-222222222222\"";
                }
            }
            else
            {
                if (!request.RequestUri.Query.Contains("sessionId=22222222-2222-2222-2222-222222222222") || !request.RequestUri.Query.Contains("minutes=180&maxCount=36")) throw new Exception("Wrong history query");
                if (Expire > 0) { Expire--; result = "{\"Code\":\"SessionNotValid\"}"; status = HttpStatusCode.InternalServerError; }
                else result = "[{\"Value\":120,\"Trend\":\"DoubleUp\",\"ST\":\"Date(1700000300000)\"},{\"Value\":100,\"Trend\":\"Flat\",\"DT\":\"Date(1700000000000+0100)\"},{\"Value\":100,\"Trend\":\"Flat\",\"DT\":\"Date(1700000000000+0100)\"}]";
            }
            return new HttpResponseMessage(status) { Content = new StringContent(result, Encoding.UTF8, "application/json") };
        }
    }
}
