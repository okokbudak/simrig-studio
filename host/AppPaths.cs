// Where things are, in the two layouts SimRigStudio.exe runs in:
//   release  (installer)  <app dir>\SimRigStudio.exe, node\node.exe, app\main.mjs, node_modules\;
//                         settings and logs in %LOCALAPPDATA%\SimRig Studio
//   dev      (git repo)   <repo>\...\SimRigStudio.exe next to app\main.mjs (the repo root holds app\);
//                         Node from the PATH, data\ and logs\ in the repo

sealed class AppPaths
{
    public required bool Release { get; init; }
    /** Install dir (release) or repo root (dev): the folder that holds app\main.mjs. */
    public required string Root { get; init; }
    public required string Node { get; init; }
    public required string Data { get; init; }
    public required string Logs { get; init; }

    public string Main => Path.Combine(Root, @"app\main.mjs");

    public static AppPaths Detect()
    {
        string exeDir = AppContext.BaseDirectory.TrimEnd('\\');
        if (File.Exists(Path.Combine(exeDir, @"node\node.exe")) && File.Exists(Path.Combine(exeDir, @"app\main.mjs")))
        {
            string user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimRig Studio");
            return new AppPaths
            {
                Release = true,
                Root = exeDir,
                Node = Path.Combine(exeDir, @"node\node.exe"),
                Data = Path.Combine(user, "data"),
                Logs = Path.Combine(user, "logs"),
            };
        }
        string root = FindRepo(exeDir);
        return new AppPaths
        {
            Release = false,
            Root = root,
            Node = "node",
            Data = Path.Combine(root, "data"),
            Logs = Path.Combine(root, "logs"),
        };
    }

    /** The first parent of the exe that holds app\main.mjs. */
    static string FindRepo(string exeDir)
    {
        for (var d = new DirectoryInfo(exeDir); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, @"app\main.mjs"))) return d.FullName;
        return Path.GetFullPath(Path.Combine(exeDir, ".."));
    }
}
