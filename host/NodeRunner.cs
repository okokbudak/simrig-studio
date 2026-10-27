// Runs the Node app (app\main.mjs) and brings it back when it ends. Its output goes to logs\studio.log.

using System.Diagnostics;

sealed class NodeRunner(AppPaths paths)
{
    public const int StudioPort = 62870;

    readonly object _lock = new();
    Process? _proc;
    TextWriter? _log;
    bool _stopping;
    int _quickExits;

    public bool Running { get { lock (_lock) return _proc is { HasExited: false }; } }

    public void Start()
    {
        lock (_lock)
        {
            _stopping = false;
            _log ??= TextWriter.Synchronized(Log.Open(Path.Combine(paths.Logs, "studio.log")));
            Launch();
        }
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Stop()
    {
        Process? p;
        lock (_lock)
        {
            _stopping = true;
            p = _proc;
            _proc = null;
        }
        try
        {
            if (p is { HasExited: false })
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(3000);
            }
        }
        catch (Exception e) { Console.WriteLine($"node: stop failed: {e.Message}"); }
    }

    void Launch()
    {
        if (!File.Exists(paths.Main))
        {
            Console.WriteLine($"node: {paths.Main} not found");
            return;
        }
        var psi = new ProcessStartInfo(paths.Node)
        {
            WorkingDirectory = paths.Root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(paths.Main);
        psi.Environment["SIMRIG_DATA"] = paths.Data;
        psi.Environment["SIMRIG_STUDIO_PORT"] = StudioPort.ToString();
        psi.Environment["SIMRIG_SERIAL_PORT"] = "62872";
        psi.Environment["TELEMETRY_BRIDGE_PORT"] = "62871";

        Process p;
        try
        {
            p = Process.Start(psi) ?? throw new InvalidOperationException("no process");
        }
        catch (Exception e)
        {
            Console.WriteLine($"node: could not start {paths.Node}: {e.Message}");
            return;
        }
        _proc = p;
        DateTime started = DateTime.UtcNow;
        p.OutputDataReceived += (_, e) => { if (e.Data != null) _log?.WriteLine($"{DateTime.Now:HH:mm:ss} {e.Data}"); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) _log?.WriteLine($"{DateTime.Now:HH:mm:ss} {e.Data}"); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.EnableRaisingEvents = true;
        p.Exited += (_, _) =>
        {
            lock (_lock)
            {
                if (_stopping || _proc != p) return;
                _quickExits = DateTime.UtcNow - started < TimeSpan.FromSeconds(10) ? _quickExits + 1 : 0;
            }
            Console.WriteLine($"node: ended (exit code {SafeExitCode(p)}), starting again");
            // a crash loop is slowed down: 2 s, then up to a minute
            int wait = Math.Min(60, 2 << Math.Min(_quickExits, 5));
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(wait));
                lock (_lock)
                {
                    if (_stopping || _proc != p) return;
                    Launch();
                }
            });
        };
    }

    static string SafeExitCode(Process p)
    {
        try { return p.ExitCode.ToString(); } catch (InvalidOperationException) { return "?"; }
    }
}
