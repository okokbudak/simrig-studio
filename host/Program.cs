// SimRig Studio host. Start it once (or with Windows); it sits in the tray, owns the COM ports, reads the game's
// telemetry and runs the Node app that serves the Studio page. See README.md.
//
//   SimRigStudio.exe                 start (or, if already running, open the Studio page)
//   SimRigStudio.exe --quit          stop the running program
//   SimRigStudio.exe --restart       restart the Node app
//   SimRigStudio.exe --status
//   SimRigStudio.exe --install-plugin   copy the SCS telemetry plugin into ETS2 / ATS (installer, as admin)

static class Program
{
    static readonly string[] Commands = ["--quit", "--restart", "--status"];

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--install-plugin") return PluginCheck.Install();
        if (args.Length > 0 && Commands.Contains(args[0]))
        {
            string? reply = ControlPort.Send(args[0].TrimStart('-'));
            // A GUI exe has no console: write to redirected stdout if there is one (scripts capturing the reply),
            // else into the calling console.
            if (GetStdHandle(-11) == IntPtr.Zero) AttachConsole(-1);
            Console.WriteLine(reply ?? "SimRig Studio is not running");
            return reply == null || reply.StartsWith("error") ? 1 : 0;
        }

        using var mutex = new Mutex(true, @"Local\SimRigStudio.Host", out bool first);
        if (!first)
        {
            ControlPort.Send("show");
            return 0;
        }

        var paths = AppPaths.Detect();
        Log.Init(paths.Logs);
        Console.WriteLine($"SimRig Studio starting ({(paths.Release ? "release" : "dev")}), app {paths.Root}, data {paths.Data}");
        Application.ThreadException += (_, e) => Console.WriteLine($"ui error: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.WriteLine($"fatal: {e.ExceptionObject}");
        ApplicationConfiguration.Initialize();

        var bridge = new TelemetryBridge();
        try { bridge.Start(); }
        catch (Exception e) { Console.WriteLine($"bridge: disabled ({e.Message}); is another SimRig Studio running, or are ports 62870-62879 reserved by Windows?"); }
        try { new SerialHub().Start(); }
        catch (Exception e) { Console.WriteLine($"serial hub: disabled ({e.Message})"); }

        var node = new NodeRunner(paths);
        var app = new TrayApp(node, bridge, paths, quiet: args.Contains("--autostart"));
        try { ControlPort.Listen(cmd => Handle(cmd, node, app)); }
        catch (Exception e) { Console.WriteLine($"control port: disabled ({e.Message}); SimRigStudio.exe --quit will not work"); }
        node.Start();
        Application.Run(app);
        node.Stop();
        Console.WriteLine("SimRig Studio stopped");
        return 0;
    }

    static string Handle(string cmd, NodeRunner node, TrayApp app)
    {
        switch (cmd.Trim())
        {
            case "quit": app.Post(app.Exit); return "ok";
            case "show": app.Post(app.OpenStudio); return "ok";
            case "restart": node.Restart(); return "ok";
            case "status": return node.Running ? "studio=running" : "studio=stopped";
            default: return $"error: unknown command '{cmd}'";
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern bool AttachConsole(int processId);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    static extern IntPtr GetStdHandle(int handle);
}
