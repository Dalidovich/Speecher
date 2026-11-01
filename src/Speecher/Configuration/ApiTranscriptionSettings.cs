using System.Net.Http.Headers;

namespace Speecher.Configuration;

public sealed class ApiTranscriptionSettings
{
    public string Endpoint { get; set; } = string.Empty;

    public string RequestMethod { get; set; } = string.Empty;

    public string HealthEndpoint { get; set; } = string.Empty;

    public string HealthMethod { get; set; } = string.Empty;

    public string HealthResponseProperty { get; set; } = string.Empty;

    public string HealthResponseValue { get; set; } = string.Empty;

    public string AudioContentType { get; set; } = string.Empty;

    public string LanguageParameter { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string TextResponseProperty { get; set; } = string.Empty;

    public string LanguageResponseProperty { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; }

    public Dictionary<string, string> Headers { get; set; } = [];

    public void FillMissingValues()
    {
        Endpoint ??= string.Empty;
        RequestMethod ??= string.Empty;
        HealthEndpoint ??= string.Empty;
        HealthMethod ??= string.Empty;
        HealthResponseProperty ??= string.Empty;
        HealthResponseValue ??= string.Empty;
        AudioContentType ??= string.Empty;
        LanguageParameter ??= string.Empty;
        Language ??= string.Empty;
        TextResponseProperty ??= string.Empty;
        LanguageResponseProperty ??= string.Empty;
        Headers ??= [];
    }

    public string? FindInvalidField()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) || !IsHttp(endpoint))
        {
            return nameof(Endpoint);
        }

        if (!IsValidMethod(RequestMethod))
        {
            return nameof(RequestMethod);
        }

        if (HealthEndpoint.Length > 0 && (!Uri.TryCreate(HealthEndpoint, UriKind.Absolute, out var healthEndpoint) || !IsHttp(healthEndpoint)))
        {
            return nameof(HealthEndpoint);
        }

        if (HealthEndpoint.Length > 0 && !IsValidMethod(HealthMethod))
        {
            return nameof(HealthMethod);
        }

        if ((HealthResponseProperty.Length == 0) != (HealthResponseValue.Length == 0))
        {
            return HealthResponseProperty.Length == 0 ? nameof(HealthResponseProperty) : nameof(HealthResponseValue);
        }

        if (!MediaTypeHeaderValue.TryParse(AudioContentType, out _))
        {
            return nameof(AudioContentType);
        }

        if (TextResponseProperty.Length == 0 || TimeoutSeconds < 0)
        {
            return TextResponseProperty.Length == 0 ? nameof(TextResponseProperty) : nameof(TimeoutSeconds);
        }

        return Headers.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value.Contains('\r') || pair.Value.Contains('\n'))
            ? nameof(Headers)
            : null;
    }

    private static bool IsHttp(Uri uri) => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsValidMethod(string method)
    {
        try
        {
            _ = new HttpMethod(method);
            return method.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
