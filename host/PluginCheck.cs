// The SCS telemetry plugin (scs-telemetry.dll) lives in each game's bin\win_x64\plugins folder. A game verify/update
// can delete it, and without it the game sends SimRig Studio nothing. This finds the games where it is missing and puts
// it back; the installer's own copy stays the source. A plugin that is there but differs (another telemetry tool's
// build) is left alone.

using System.Diagnostics;

static class PluginCheck
{
    public sealed record Problem(string Game, string GameDir);

    static string Shipped => Path.Combine(AppContext.BaseDirectory, @"plugin\scs-telemetry.dll");

    static string Target(string gameDir) => Path.Combine(gameDir, @"bin\win_x64\plugins\scs-telemetry.dll");

    /** Installed games that have no telemetry plugin. */
    public static List<Problem> Find()
    {
        var (ets2, ats) = GameFinder.FindGames();
        var list = new List<Problem>();
        foreach (var (name, dir) in new[] { ("ETS2", ets2), ("ATS", ats) })
            if (dir != null && !File.Exists(Target(dir))) list.Add(new Problem(name, dir));
        return list;
    }

    public static bool GameRunning() =>
        Process.GetProcessesByName("eurotrucks2").Length > 0 || Process.GetProcessesByName("amtrucks").Length > 0;

    /** Copies the plugin into every game that lacks it; returns how many could not be done (no rights). */
    public static int CopyMissing()
    {
        if (!File.Exists(Shipped)) return 1;
        int failed = 0;
        foreach (var p in Find())
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Target(p.GameDir))!);
                File.Copy(Shipped, Target(p.GameDir), overwrite: false);
            }
            catch (Exception) { failed++; }
        }
        return failed;
    }

    /** The installer's step (as admin): plugin into every installed game, an existing one is replaced. */
    public static int Install()
    {
        if (!File.Exists(Shipped)) return 2;
        var (ets2, ats) = GameFinder.FindGames();
        int failed = 0;
        foreach (var game in new[] { ets2, ats })
        {
            if (game == null) continue;
            try
            {
                string dir = Path.Combine(game, @"bin\win_x64\plugins");
                Directory.CreateDirectory(dir);
                File.Copy(Shipped, Path.Combine(dir, "scs-telemetry.dll"), overwrite: true);
            }
            catch (Exception) { failed++; } // game running (dll in use) or no rights
        }
        return failed == 0 ? 0 : 1;
    }
}
