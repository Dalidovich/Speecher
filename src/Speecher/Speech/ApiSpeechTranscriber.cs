using System.Net.Http.Headers;
using System.Text.Json;
using Speecher.Configuration;
using Speecher.Transcription;

namespace Speecher.Speech;

public sealed class ApiSpeechTranscriber : ISpeechTranscriber
{
    private readonly ApiTranscriptionSettings settings;
    private readonly HttpClient client;
    private readonly SemaphoreSlim gate = new(1, 1);

    private ApiSpeechTranscriber(ApiTranscriptionSettings settings)
    {
        this.settings = settings;
        client = new HttpClient
        {
            Timeout = settings.TimeoutSeconds == 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromSeconds(settings.TimeoutSeconds)
        };

        foreach (var header in settings.Headers)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    public string Backend => "API";

    public static async Task<ApiSpeechTranscriber> CreateAsync(ApiTranscriptionSettings settings, CancellationToken cancellationToken)
    {
        var transcriber = new ApiSpeechTranscriber(settings);
        try
        {
            await transcriber.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
            return transcriber;
        }
        catch
        {
            transcriber.Dispose();
            throw;
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(float[] samples, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var content = new ByteArrayContent(CreateWav(samples));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(settings.AudioContentType);
            using var request = new HttpRequestMessage(new HttpMethod(settings.RequestMethod), BuildRequestUri())
            {
                Content = content
            };
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
            var text = GetRequiredString(document.RootElement, settings.TextResponseProperty);
            var language = GetOptionalString(document.RootElement, settings.LanguageResponseProperty) ?? "unknown";
            return new TranscriptionResult(text.Trim(), language);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        client.Dispose();
        gate.Dispose();
    }

    private async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (settings.HealthEndpoint.Length == 0)
        {
            return;
        }

        using var request = new HttpRequestMessage(new HttpMethod(settings.HealthMethod), settings.HealthEndpoint);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (settings.HealthResponseProperty.Length == 0)
        {
            return;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        var value = GetRequiredString(document.RootElement, settings.HealthResponseProperty);
        if (!string.Equals(value, settings.HealthResponseValue, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"API health response property \"{settings.HealthResponseProperty}\" has an unexpected value.");
        }
    }

    private Uri BuildRequestUri()
    {
        if (settings.LanguageParameter.Length == 0)
        {
            return new Uri(settings.Endpoint, UriKind.Absolute);
        }

        var separator = settings.Endpoint.Contains('?') ? '&' : '?';
        var uri = $"{settings.Endpoint}{separator}{Uri.EscapeDataString(settings.LanguageParameter)}={Uri.EscapeDataString(settings.Language)}";
        return new Uri(uri, UriKind.Absolute);
    }

    private static string GetRequiredString(JsonElement element, string property)
    {
        var value = GetOptionalString(element, property);
        return value ?? throw new InvalidDataException($"API response does not contain a string \"{property}\" property.");
    }

    private static string? GetOptionalString(JsonElement element, string property)
    {
        return property.Length > 0 && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static byte[] CreateWav(float[] samples)
    {
        var dataLength = checked(samples.Length * sizeof(short));
        var bytes = new byte[44 + dataLength];
        var span = bytes.AsSpan();
        "RIFF"u8.CopyTo(span);
        BitConverter.TryWriteBytes(span[4..], 36 + dataLength);
        "WAVEfmt "u8.CopyTo(span[8..]);
        BitConverter.TryWriteBytes(span[16..], 16);
        BitConverter.TryWriteBytes(span[20..], (short)1);
        BitConverter.TryWriteBytes(span[22..], (short)1);
        BitConverter.TryWriteBytes(span[24..], 16000);
        BitConverter.TryWriteBytes(span[28..], 32000);
        BitConverter.TryWriteBytes(span[32..], (short)2);
        BitConverter.TryWriteBytes(span[34..], (short)16);
        "data"u8.CopyTo(span[36..]);
        BitConverter.TryWriteBytes(span[40..], dataLength);

        for (var i = 0; i < samples.Length; i++)
        {
            var sample = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            BitConverter.TryWriteBytes(span[(44 + i * sizeof(short))..], sample);
        }

        return bytes;
    }
}
