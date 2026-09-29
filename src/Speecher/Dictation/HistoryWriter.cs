using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Speecher.App;

namespace Speecher.Dictation;

public sealed class HistoryWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly object sync = new();
    private bool disabled;

    public void Append(string text, string language, double audioSeconds)
    {
        var entry = new HistoryEntry(
            DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
            language,
            Math.Round(audioSeconds, 2),
            text);
        var line = JsonSerializer.Serialize(entry, SerializerOptions) + "\n";

        lock (sync)
        {
            if (disabled)
            {
                return;
            }

            try
            {
                File.AppendAllText(PortablePaths.HistoryFile, line, Utf8WithoutBom);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                disabled = true;
                ConsoleLog.Warning("Application folder is not writable, history.jsonl will not be written.");
            }
        }
    }

    private sealed record HistoryEntry(
        [property: JsonPropertyName("timestamp")] string Timestamp,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("audioSeconds")] double AudioSeconds,
        [property: JsonPropertyName("text")] string Text);
}
