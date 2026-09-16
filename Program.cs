using System.Diagnostics;

namespace Drift;

// watch a path, run a command every time something changes.
// debounced so rapid saves don't fire the command 20 times.

static class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2) { Help(); return; }

        string watchPath = args[0];
        string command = args[1];
        string[] cmdArgs = args[2..];

        string? filter = null;
        bool recursive = true;
        int debounceMs = 300;

        for (int i = 0; i < cmdArgs.Length; i++)
        {
            switch (cmdArgs[i])
            {
                case "-f" or "--filter":
                    if (i + 1 < cmdArgs.Length) filter = cmdArgs[++i];
                    break;
                case "--no-recursive":
                    recursive = false;
                    break;
                case "-d" or "--debounce":
                    if (i + 1 < cmdArgs.Length && int.TryParse(cmdArgs[++i], out int ms))
                        debounceMs = ms;
                    break;
            }
        }

        if (!Directory.Exists(watchPath) && !File.Exists(watchPath))
        {
            Err($"path not found: {watchPath}");
            return;
        }

        string dir = Directory.Exists(watchPath) ? watchPath : Path.GetDirectoryName(watchPath)!;
        string fileFilter = filter ?? (File.Exists(watchPath) ? Path.GetFileName(watchPath) : "*");

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Log($"watching {Cyan(watchPath)}  filter={Dim(fileFilter)}  cmd={Dim(command)}");
        Log(Dim("ctrl+c to stop\n"));

        Timer? debounce = null;
        object dlock = new();

        using var watcher = new FileSystemWatcher(dir, fileFilter)
        {
            IncludeSubdirectories = recursive,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true,
        };

        void OnChange(object _, FileSystemEventArgs e)
        {
            lock (dlock)
            {
                debounce?.Dispose();
                debounce = new Timer(_ => RunCommand(command, e.FullPath), null, debounceMs, Timeout.Infinite);
            }
        }

        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Deleted += OnChange;
        watcher.Renamed += (_, e) => OnChange(null!, e);

        // block forever
        var done = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.Set(); };
        done.Wait();
        Log(Dim("\nstopped."));
    }

    static void RunCommand(string command, string changedFile)
    {
        string ts = DateTime.Now.ToString("HH:mm:ss");
        Log($"\n  [{ts}]  {Cyan(Path.GetFileName(changedFile))} changed");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var p = Process.Start(psi)!;
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (stdout.Length > 0) Console.Write(Indent(stdout.TrimEnd()));
            if (stderr.Length > 0) Console.Write(Dim(Indent(stderr.TrimEnd())));
            Console.WriteLine();

            string status = p.ExitCode == 0
                ? $"\x1b[32m✓ exit 0\x1b[0m"
                : $"\x1b[31m✗ exit {p.ExitCode}\x1b[0m";
            Log($"  {status}");
        }
        catch (Exception ex)
        {
            Err($"failed to run command: {ex.Message}");
        }
    }

    static string Indent(string s) => "\n" + string.Join("\n", s.Split('\n').Select(l => "    " + l)) + "\n";
    static string Cyan(string s) => $"\x1b[36m{s}\x1b[0m";
    static string Dim(string s) => $"\x1b[2m{s}\x1b[0m";
    static void Log(string msg) => Console.WriteLine($"  {msg}");
    static void Err(string msg) => Console.Error.WriteLine($"  \x1b[31merror:\x1b[0m {msg}");

    static void Help() => Console.WriteLine("""

  drift — watch files and run a command on change

  usage:
    drift <path> <command> [flags]

  flags:
    -f, --filter <glob>      only watch files matching glob (default: *)
        --no-recursive       don't watch subdirectories
    -d, --debounce <ms>      debounce delay in milliseconds (default: 300)

  examples:
    drift src/ "dotnet build"
    drift . "npm test" -f *.ts
    drift config.json "echo reloading..."
    drift src/ "dotnet run" --debounce 500

""");
}
