using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Speecher.App;
using Speecher.Interop;

namespace Speecher.Speech;

public static partial class CudaRuntimeProvider
{
    private const string RedistBaseUrl = "https://developer.download.nvidia.com/compute/cuda/redist/";
    private const string RedistManifest = "redistrib_12.9.1.json";
    private const string Platform = "windows-x86_64";

    private static readonly CudaPackage[] Packages =
    [
        new("cuda_cudart", ["cudart64_12.dll"], CudartPattern()),
        new("libcublas", ["cublasLt64_12.dll", "cublas64_12.dll"], CublasPattern())
    ];

    private const string CompilationCacheMaxBytes = "4294967296";

    public static void RedirectCompilationCache()
    {
        Environment.SetEnvironmentVariable("CUDA_CACHE_PATH", Path.Combine(PortablePaths.CudaDirectory, "cache"));
        Environment.SetEnvironmentVariable("CUDA_CACHE_MAXSIZE", CompilationCacheMaxBytes);
    }

    public static bool IsNvidiaDriverPresent()
    {
        if (!NativeLibrary.TryLoad("nvcuda.dll", out var handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }

    public static async Task EnsureLibrariesAsync(FileDownloader downloader, CancellationToken cancellationToken)
    {
        var missing = Packages.Where(package => !package.IsInstalled()).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        ConsoleLog.Info("Downloading CUDA runtime libraries...");
        try
        {
            Directory.CreateDirectory(PortablePaths.CudaDirectory);
            var manifest = await downloader.GetStringAsync(RedistBaseUrl + RedistManifest, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(manifest);

            foreach (var package in missing)
            {
                await InstallPackageAsync(downloader, document.RootElement, package, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            CleanUpArtifacts();
            ConsoleLog.Warning($"Failed to download CUDA runtime libraries, falling back to CPU: {ConsoleLog.Describe(ex)}");
            return;
        }
        catch
        {
            CleanUpArtifacts();
            throw;
        }

        ConsoleLog.Info("CUDA runtime libraries downloaded.");
    }

    public static void Activate()
    {
        if (!Packages.All(package => package.IsInstalled()))
        {
            return;
        }

        Kernel32.SetDllDirectory(PortablePaths.CudaDirectory);
        foreach (var library in Packages.SelectMany(package => package.RequiredFiles))
        {
            var path = Path.Combine(PortablePaths.CudaDirectory, library);
            if (!NativeLibrary.TryLoad(path, out _))
            {
                ConsoleLog.Warning($"Failed to load {library}.");
            }
        }
    }

    private static async Task InstallPackageAsync(FileDownloader downloader, JsonElement manifest, CudaPackage package, CancellationToken cancellationToken)
    {
        var entry = manifest.GetProperty(package.Name).GetProperty(Platform);
        var relativePath = entry.GetProperty("relative_path").GetString()
            ?? throw new InvalidDataException($"Missing archive path for {package.Name}.");
        var sha256 = entry.GetProperty("sha256").GetString();

        var archivePath = Path.Combine(PortablePaths.CudaDirectory, Path.GetFileName(relativePath));
        await downloader.DownloadAsync(RedistBaseUrl + relativePath, archivePath, sha256, cancellationToken).ConfigureAwait(false);

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var archiveEntry in archive.Entries.Where(e => package.ExtractPattern.IsMatch(e.Name)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = Path.Combine(PortablePaths.CudaDirectory, archiveEntry.Name);
                var partialPath = targetPath + ".part";
                archiveEntry.ExtractToFile(partialPath, overwrite: true);
                File.Move(partialPath, targetPath, overwrite: true);
            }
        }
        finally
        {
            File.Delete(archivePath);
        }

        if (!package.IsInstalled())
        {
            throw new InvalidDataException($"Archive {Path.GetFileName(relativePath)} does not contain the required libraries.");
        }
    }

    private static void CleanUpArtifacts()
    {
        if (!Directory.Exists(PortablePaths.CudaDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(PortablePaths.CudaDirectory)
                     .Where(f => f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    [GeneratedRegex(@"^cudart64_\d+\.dll$", RegexOptions.IgnoreCase)]
    private static partial Regex CudartPattern();

    [GeneratedRegex(@"^cublas(Lt)?64_\d+\.dll$", RegexOptions.IgnoreCase)]
    private static partial Regex CublasPattern();

    private sealed record CudaPackage(string Name, string[] RequiredFiles, Regex ExtractPattern)
    {
        public bool IsInstalled() => RequiredFiles.All(file => File.Exists(Path.Combine(PortablePaths.CudaDirectory, file)));
    }
}
