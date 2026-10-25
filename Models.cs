using System.Globalization;
using System.Text.Json;

namespace GlucoGeek;

internal static class Wire
{
    public static JsonElement Get(this JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? v : default;
    public static string Text(this JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
    public static double? Number(this JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var n) && double.IsFinite(n) ? n : null;
    public static IEnumerable<JsonElement> Items(this JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray() : [];
    public static DateTimeOffset? Date(string text, bool utc)
    {
        string[] formats = ["M/d/yyyy h:mm:ss tt", "M/d/yyyy H:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];
        return DateTimeOffset.TryParseExact(text, formats, CultureInfo.InvariantCulture,
            utc ? DateTimeStyles.AssumeUniversal : DateTimeStyles.AssumeLocal, out var result) ? result.ToUniversalTime() : null;
    }
}

internal record Reading(DateTimeOffset At, double Mgdl, int Trend, bool High = false, bool Low = false, string? TrendDescription = null)
{
    public string Arrow => Trend switch { 1 => "↓", 2 => "↘", 3 => "→", 4 => "↗", 5 => "↑", 6 => "↓↓", 7 => "↑↑", _ => "" };
    public string TrendText => TrendDescription ?? (Trend switch { 1 => "Falling quickly", 2 => "Falling", 3 => "Steady", 4 => "Rising", 5 => "Rising quickly", 6 => "Falling quickly", 7 => "Rising quickly", _ => "Trend unavailable" });
    public bool Stale(DateTimeOffset now) => now - At > TimeSpan.FromMinutes(11) || At > now.AddMinutes(2);
    public static Reading? Parse(JsonElement e)
    {
        // Never guess units from the magnitude of a glucose value.
        var mg = e.Get("ValueInMgPerDl").Number();
        if (mg is null && e.Get("Value").Number() is double v)
            mg = e.Get("GlucoseUnits").Number() switch { 1 => v, 2 => v * 18.0182, _ => null };
        var at = Wire.Date(e.Get("FactoryTimestamp").Text(), true) ?? Wire.Date(e.Get("Timestamp").Text(), false);
        if (mg is null or <= 0 || at is null) return null;
        return new(at.Value, mg.Value, (int)(e.Get("TrendArrow").Number() ?? 0),
            e.Get("isHigh").ValueKind == JsonValueKind.True, e.Get("isLow").ValueKind == JsonValueKind.True);
    }
}

internal record Snapshot(Reading Current, List<Reading> History, string Patient, double Low, double High, bool Mmol, DateTimeOffset? Sensor)
{
    public static string Format(double mgdl, bool mmol) => (mmol ? mgdl / 18.0182 : mgdl).ToString(mmol ? "F1" : "F0", CultureInfo.CurrentCulture);
    public Reading? Previous => History.LastOrDefault(r => r.At < Current.At);
    public List<Reading> Window => History.Where(r => r.At >= Current.At.AddHours(-3) && r.At <= Current.At).ToList();
    public static Snapshot Parse(JsonElement data)
    {
        var c = data.Get("connection");
        var m = c.Get("glucoseMeasurement");
        var current = Reading.Parse(m) ?? throw new InvalidDataException("No valid glucose reading with a timestamp and known units is available.");
        var history = data.Get("graphData").Items().Select(Reading.Parse).OfType<Reading>().Where(r => r.At < current.At).Append(current).GroupBy(r => r.At).Select(g => g.Last()).OrderBy(r => r.At).ToList();
        var low = c.Get("targetLow").Number() ?? 70;
        var high = c.Get("targetHigh").Number() ?? 180;
        if (low <= 0 || high <= low) throw new InvalidDataException("LibreLinkUp returned an invalid target range.");
        var value = m.Get("Value").Number();
        var mg = m.Get("ValueInMgPerDl").Number();
        bool mmol = value is not null && mg is not null ? Math.Abs(value.Value - mg.Value) >= 0.5 : m.Get("GlucoseUnits").Number() == 2;
        var seconds = data.Get("activeSensors").Items().Select(s => s.Get("sensor").Get("a").Number()).FirstOrDefault(s => s.HasValue) ?? c.Get("sensor").Get("a").Number();
        DateTimeOffset? sensor = seconds is > 0 and < 253402300799 ? DateTimeOffset.FromUnixTimeSeconds((long)seconds.Value) : null;
        return new(current, history, Name(c), low, high, mmol, sensor);
    }
    public static string Name(JsonElement c) => (c.Get("firstName").Text() + " " + c.Get("lastName").Text()).Trim();
}
