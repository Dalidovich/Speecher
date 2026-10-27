using System.ComponentModel;
using System.Diagnostics;
using Speecher.App;
using Speecher.Configuration;

namespace Speecher.Dictation;

public static class ScriptLauncher
{
    public static void Launch(ScriptRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Command))
        {
            LaunchFile(rule.Path);
        }
        else
        {
            RunCommand(rule.Command, rule.Shell);
        }
    }

    private static void LaunchFile(string path)
    {
        var target = Resolve(path);
        var startInfo = new ProcessStartInfo(target)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.IsPathRooted(target)
                ? Path.GetDirectoryName(target) ?? PortablePaths.Root
                : PortablePaths.Root
        };

        try
        {
            using var process = Process.Start(startInfo);
            ConsoleLog.Event($"Script started: {target}");
        }
        catch (Win32Exception ex)
        {
            ConsoleLog.Error($"Failed to start script: {ex.Message}");
        }
    }

    private static void RunCommand(string command, ScriptShell shell)
    {
        var startInfo = CreateShellStartInfo(command, shell);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WorkingDirectory = PortablePaths.Root;

        try
        {
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Exited += (_, _) => ReportExit(process, command);
            process.Start();
            ConsoleLog.Event($"{shell} command started: {command}");
        }
        catch (Win32Exception ex)
        {
            ConsoleLog.Error($"Failed to start {shell} command: {ex.Message}");
        }
    }

    private static ProcessStartInfo CreateShellStartInfo(string command, ScriptShell shell)
    {
        if (shell == ScriptShell.PowerShell)
        {
            return new ProcessStartInfo("powershell.exe")
            {
                ArgumentList = { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command }
            };
        }

        return new ProcessStartInfo("cmd.exe", $"/d /s /c \"{command}\"");
    }

    private static void ReportExit(Process process, string command)
    {
        using (process)
        {
            if (process.ExitCode != 0)
            {
                ConsoleLog.Warning($"Command exited with code {process.ExitCode}: {command}");
            }
        }
    }

    private static string Resolve(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        var besideApp = Path.GetFullPath(expanded, PortablePaths.Root);
        var hasDirectory = expanded.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0;
        return hasDirectory || File.Exists(besideApp) ? besideApp : expanded;
    }
}
