using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MacClipboardMonitor.Services;

/// <summary>Versionado M.M.M (major.minor.patch). Fuente única: AssemblyVersion/InformationalVersion.</summary>
public static class AppVersion
{
    private static readonly Regex SemVer = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

    /// <summary>Version actual del binario (del assembly). Si no hay versión válida, 0.0.0.</summary>
    public static Version Current
    {
        get
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v is null) return new Version(0, 0, 0);
                // Normalizar a M.M.M (ignorar revision)
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
            catch
            {
                return new Version(0, 0, 0);
            }
        }
    }

    public static string CurrentString => $"{Current.Major}.{Current.Minor}.{Current.Build}";

    public static bool IsValid(Version v) =>
        v != null && !(v.Major == 0 && v.Minor == 0 && v.Build == 0) && SemVer.IsMatch($"{v.Major}.{v.Minor}.{v.Build}");

    public static bool IsValidString(string? s) =>
        !string.IsNullOrWhiteSpace(s) && SemVer.IsMatch(s.Trim());

    public static bool TryParse(string? s, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (!SemVer.IsMatch(s)) return false;
        var parts = s.Split('.');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var major)) return false;
        if (!int.TryParse(parts[1], out var minor)) return false;
        if (!int.TryParse(parts[2], out var patch)) return false;
        version = new Version(major, minor, patch);
        return true;
    }

    /// <summary>Compara M.M.M. Retorna -1,0,1.</summary>
    public static int Compare(Version a, Version b)
    {
        if (a.Major != b.Major) return a.Major.CompareTo(b.Major);
        if (a.Minor != b.Minor) return a.Minor.CompareTo(b.Minor);
        return a.Build.CompareTo(b.Build);
    }
}
