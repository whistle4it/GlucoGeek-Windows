using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace GlucoGeek;

internal sealed class Settings
{
    public string Provider { get; set; } = "LibreLinkUp";
    public string DexcomUsername { get; set; } = "";
    public string DexcomPasswordProtected { get; set; } = "";
    public string DexcomRegion { get; set; } = "";
    public double DexcomLow { get; set; } = 70;
    public double DexcomHigh { get; set; } = 180;
    public bool HasCredentials => Provider == "Dexcom G7" ? DexcomPasswordProtected.Length > 0 : PasswordProtected.Length > 0;
    public string DexcomPassword => DexcomPasswordProtected.Length == 0 ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(DexcomPasswordProtected), null, DataProtectionScope.CurrentUser));
    public void SetDexcomPassword(string password) => DexcomPasswordProtected = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser));
    public string Email { get; set; } = "";
    public string PasswordProtected { get; set; } = "";
    public string Patient { get; set; } = "";
    public string Unit { get; set; } = "Automatic";
    public int Minutes { get; set; } = 1;
    public bool ShowTrend { get; set; } = true;
    public bool Colour { get; set; } = true;
    public string ApiVersion { get; set; } = "4.16.0";
    public string ApiProduct { get; set; } = "llu.ios";
    public string UserAgent { get; set; } = "Mozilla/5.0 (iPhone; CPU OS 17_4.1 like Mac OS X) AppleWebKit/536.26 (KHTML, like Gecko) Version/17.4.1 Mobile/10A5355d Safari/8536.25";
    // Preserve the original profile so upgrading keeps both providers' protected credentials.
    public static string Location => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Librewin", "settings.json");
    public static Settings Load() => File.Exists(Location) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(Location)) ?? new() : new();
    public string Password => PasswordProtected.Length == 0 ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(PasswordProtected), null, DataProtectionScope.CurrentUser));
    public void SetPassword(string password) => PasswordProtected = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser));
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
        File.WriteAllText(Location + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(Location + ".tmp", Location, true);
    }
    public Settings Copy() => (Settings)MemberwiseClone();
    public bool IsMmol(Snapshot snapshot) => Unit == "mmol/L" || Unit == "Automatic" && snapshot.Mmol;
    public static bool Startup
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("GlucoGeek") is string || key?.GetValue("Librewin") is string; }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (value) key.SetValue("GlucoGeek", "\"" + Application.ExecutablePath + "\" --background");
            else key.DeleteValue("GlucoGeek", false);
            key.DeleteValue("Librewin", false);
        }
    }
}
