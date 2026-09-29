using System.Security.Cryptography;

namespace Speecher.Speech;

public sealed class FileDownloader : IDisposable
{
    private const string PartialSuffix = ".part";

    private readonly HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static void DeletePartial(string targetPath) => DeleteIfExists(targetPath + PartialSuffix);

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        return await client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadAsync(string url, string targetPath, string? expectedSha256, CancellationToken cancellationToken)
    {
        var partialPath = targetPath + PartialSuffix;
        try
        {
            await using (var file = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1 << 20];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                var actualSha256 = Convert.ToHexString(hash.GetHashAndReset());
                if (expectedSha256 is not null && !string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Checksum mismatch for {Path.GetFileName(targetPath)}.");
                }
            }

            File.Move(partialPath, targetPath, overwrite: true);
        }
        catch
        {
            DeleteIfExists(partialPath);
            throw;
        }
    }

    public void Dispose() => client.Dispose();

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
