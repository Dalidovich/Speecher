namespace Speecher.App;

public static class PortablePaths
{
    public static string ExecutablePath { get; } = ResolveExecutablePath();

    public static string Root { get; } = Path.GetDirectoryName(ExecutablePath) ?? Directory.GetCurrentDirectory();

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string HistoryFile => Path.Combine(Root, "history.jsonl");

    public static string ModelsDirectory => Path.Combine(Root, "models");

    public static string CudaDirectory => Path.Combine(Root, "runtime", "cuda");

    private static string ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            return Path.GetFullPath(processPath);
        }

        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var modulePath = process.MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(modulePath))
        {
            return Path.GetFullPath(modulePath);
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "Speecher.exe");
    }
}
