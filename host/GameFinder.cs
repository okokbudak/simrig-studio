// Finds the installed games: every Steam library, or ETS2_DIR / ATS_DIR if set.

using System.Text.RegularExpressions;
using Microsoft.Win32;

static class GameFinder
{
    public static (string? Ets2, string? Ats) FindGames()
    {
        string? ets2 = Environment.GetEnvironmentVariable("ETS2_DIR");
        string? ats = Environment.GetEnvironmentVariable("ATS_DIR");
        foreach (var lib in SteamLibraries())
        {
            string e = Path.Combine(lib, @"steamapps\common\Euro Truck Simulator 2");
            string a = Path.Combine(lib, @"steamapps\common\American Truck Simulator");
            if (ets2 == null && File.Exists(Path.Combine(e, "base.scs"))) ets2 = e;
            if (ats == null && File.Exists(Path.Combine(a, "base.scs"))) ats = a;
        }
        return (ets2, ats);
    }

    static IEnumerable<string> SteamLibraries()
    {
        string steam = (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                        ?? @"C:\Program Files (x86)\Steam").Replace('/', '\\');
        var libs = new List<string> { steam };
        string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }
        return libs.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
