using System.Runtime.InteropServices;
using Speecher.Interop;

namespace Speecher.Transcription;

public static class AudioFilePicker
{
    private const string Title = "Select audio files to transcribe";
    private const uint OptionFileMustExist = 0x1000;
    private const uint OptionPathMustExist = 0x800;
    private const uint OptionAllowMultiSelect = 0x200;
    private const uint OptionForceFileSystem = 0x40;
    private const uint FileSystemPathForm = 0x80058000;
    private const int ErrorCancelled = unchecked((int)0x800704C7);

    private static readonly FileFilterSpec[] Filters =
    [
        new("Audio and video files", "*.ogg;*.oga;*.opus;*.wav;*.mp3;*.m4a;*.aac;*.wma;*.flac;*.mp4;*.m4v;*.mov;*.wmv"),
        new("All files", "*.*")
    ];

    public static Task<IReadOnlyList<string>> PickAsync()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => PickInto(completion)) { IsBackground = true, Name = "FilePicker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static void PickInto(TaskCompletionSource<IReadOnlyList<string>> completion)
    {
        try
        {
            completion.SetResult(Pick());
        }
        catch (Exception ex)
        {
            completion.SetException(ex);
        }
    }

    private static IReadOnlyList<string> Pick()
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogClass();
        try
        {
            dialog.SetTitle(Title);
            dialog.SetFileTypes((uint)Filters.Length, Filters);
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | OptionFileMustExist | OptionPathMustExist | OptionAllowMultiSelect | OptionForceFileSystem);

            var result = dialog.Show(0);
            if (result == ErrorCancelled)
            {
                return [];
            }

            Marshal.ThrowExceptionForHR(result);
            dialog.GetResults(out var items);
            return ReadPaths(items);
        }
        finally
        {
            Marshal.FinalReleaseComObject(dialog);
        }
    }

    private static List<string> ReadPaths(IShellItemArray items)
    {
        items.GetCount(out var count);
        var paths = new List<string>((int)count);
        for (uint i = 0; i < count; i++)
        {
            items.GetItemAt(i, out var item);
            item.GetDisplayName(FileSystemPathForm, out var name);
            try
            {
                paths.Add(Marshal.PtrToStringUni(name)!);
            }
            finally
            {
                Marshal.FreeCoTaskMem(name);
            }
        }

        return paths;
    }
}
