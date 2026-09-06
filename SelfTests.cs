using System.Net;
using System.Text;
using System.Text.Json;

namespace Librewin;

internal static class SelfTests
{
    internal static Snapshot Demo()
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-2);
        var history = Enumerable.Range(0, 37).Select(i => new Reading(now.AddMinutes((i - 36) * 5), 112 + Math.Sin(i / 5.0) * 25, 3)).ToList();
        return new(history.Last(), history, "Demo patient", 70, 180, true, now.AddDays(-5));
    }
    internal static int Run(string output)
    {
        var passed = new List<string>();
        void Check(bool good, string label) { if (!good) throw new Exception(label); passed.Add("PASS " + label); }
        JsonElement Json(string s) { using var doc = JsonDocument.Parse(s); return doc.RootElement.Clone(); }
        try
        {
            Check(Snapshot.Format(180.182, true) == 10.0.ToString("F1"), "mg/dL to mmol/L conversion");
            Check(Wire.Date("9/6/2026 1:02:03 PM", true) == new DateTimeOffset(2026, 9, 6, 13, 2, 3, TimeSpan.Zero), "FactoryTimestamp uses UTC");
            Check(Wire.Date("2026-09-06T13:02:03Z", true)?.Hour == 13, "ISO timestamp");
            Check(Reading.Parse(Json("{\"Value\":5.5,\"FactoryTimestamp\":\"9/6/2026 1:02:03 PM\"}")) == null, "Ambiguous units rejected");
            Check(Reading.Parse(Json("{\"ValueInMgPerDl\":100}")) == null, "Missing timestamp rejected");
            var fallback = Reading.Parse(Json("{\"Value\":5.5,\"GlucoseUnits\":2,\"FactoryTimestamp\":\"9/6/2026 1:02:03 PM\"}"));
            Check(fallback != null && Math.Abs(fallback.Mgdl - 99.1001) < .001, "Explicit fallback units converted");
            var now = DateTimeOffset.UtcNow;
            Check(new Reading(now.AddMinutes(-12), 100, 3).Stale(now), "Old reading marked stale");
            Check(new Reading(now.AddMinutes(3), 100, 3).Stale(now), "Future timestamp flagged");
            Check(!new Reading(now.AddMinutes(-10), 100, 3).Stale(now), "Fresh reading accepted");
            Check(!LibreLinkClient.SafeHost("https://libreview.io.attacker.example"), "Regional host suffix attack blocked");
            Check(!LibreLinkClient.SafeHost("http://api.libreview.io"), "Non-TLS regional host blocked");
            Check(LibreLinkClient.SafeHost("https://api-eu.libreview.io/"), "Official regional endpoint accepted");
            var people = Json("[{\"patientId\":\"one\",\"firstName\":\"Alice\"},{\"patientId\":\"two\",\"firstName\":\"Bob\"}]").Items().ToArray();
            Check(LibreLinkClient.Choose(people, "Bob").Get("patientId").Text() == "two", "Exact patient selected");
            bool refused = false;
            try { LibreLinkClient.Choose(people, ""); } catch (InvalidOperationException) { refused = true; }
            Check(refused, "Multiple patients require explicit selection");
            refused = false;
            try { LibreLinkClient.Choose(people, "Unknown"); } catch (InvalidOperationException) { refused = true; }
            Check(refused, "Unmatched patient never silently substituted");
            var settings = new Settings { Email = "test@example.invalid", PasswordProtected = "fixture-only" };
            try
            {
                var credentialTest = new Settings(); credentialTest.SetPassword("fixture-password");
                Check(credentialTest.Password == "fixture-password" && !credentialTest.PasswordProtected.Contains("fixture-password"), "DPAPI credential round trip");
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                passed.Add("UNVERIFIED DPAPI: Windows user profile is unavailable in this execution environment. Run tests from your normal Windows session.");
            }
            using var handler = new FakeHandler();
            using var client = new LibreLinkClient(settings, handler, () => "fixture-password");
            var snapshot = client.Fetch(CancellationToken.None).GetAwaiter().GetResult();
            Check(snapshot.Patient == "Alice" && snapshot.Current.Mgdl == 108, "Login → region → connections → graph");
            Check(snapshot.Low == 70 && snapshot.High == 180 && snapshot.Mmol, "Account unit and mg/dL target range");
            Check(snapshot.History.Count == 2 && snapshot.Previous?.Mgdl == 100, "History sorted and current deduplicated");
            Check(handler.AuthenticatedRequests > 0, "Authenticated headers attached");
            handler.ExpireOnce = true;
            awaitSync(client.Fetch(CancellationToken.None));
            Check(handler.Expired, "401 refresh reauthenticates once");
            File.WriteAllLines(output, passed.Append($"{passed.Count(p => p.StartsWith("PASS"))} checks passed; {passed.Count(p => p.StartsWith("UNVERIFIED"))} unverified."));
            return 0;
        }
        catch (Exception ex) { File.WriteAllLines(output, passed.Append("FAIL " + ex)); return 1; }
    }
    private static void awaitSync(Task<Snapshot> task) => task.GetAwaiter().GetResult();
    private sealed class FakeHandler : HttpMessageHandler
    {
        public int AuthenticatedRequests;
        public bool ExpireOnce, Expired;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string data;
            if (path == "/llu/auth/login") data = request.RequestUri.Host == "api.libreview.io" ? "{\"redirect\":true,\"region\":\"eu\"}" : "{\"authTicket\":{\"token\":\"test-token\",\"expires\":4102444800},\"user\":{\"id\":\"fixture-user\"}}";
            else if (path == "/llu/config/country") data = "{\"regionalMap\":{\"eu\":{\"lslApi\":\"https://api-eu.libreview.io\"}}}";
            else
            {
                if (!request.Headers.Contains("account-id") || request.Headers.Authorization?.Parameter != "test-token") throw new Exception("Missing auth headers");
                AuthenticatedRequests++;
                if (ExpireOnce) { ExpireOnce = false; Expired = true; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); }
                data = path == "/llu/connections" ? "[{\"patientId\":\"one\",\"firstName\":\"Alice\"}]" : "{\"connection\":{\"patientId\":\"one\",\"firstName\":\"Alice\",\"targetLow\":70,\"targetHigh\":180,\"glucoseMeasurement\":{\"ValueInMgPerDl\":108,\"Value\":6,\"FactoryTimestamp\":\"9/6/2026 1:02:03 PM\"}},\"graphData\":[{\"ValueInMgPerDl\":108,\"FactoryTimestamp\":\"9/6/2026 1:02:03 PM\"},{\"ValueInMgPerDl\":100,\"FactoryTimestamp\":\"9/6/2026 12:57:03 PM\"}]}";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":0,\"data\":" + data + "}", Encoding.UTF8, "application/json") });
        }
    }
}
