using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GramCloneClient.Models;

namespace GramCloneClient.Backend;

/// <summary>
/// Lightweight HTTP client for the FastAPI backend.
/// </summary>
public sealed class BackendClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private AppSettings _settings;

    public BackendClient(AppSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
    }

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    private string BaseUrl => _settings.BackendUrl.TrimEnd('/');

    private string BuildUrl(string path, string? overrideBaseUrl = null)
    {
        string baseUrl = string.IsNullOrWhiteSpace(overrideBaseUrl) ? BaseUrl : overrideBaseUrl.TrimEnd('/');
        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }
        return $"{baseUrl}{path}";
    }

    public async Task<string> EditAsync(
        string text,
        EditingMode mode,
        ToneStyle tone,
        CancellationToken cancellationToken = default)
    {
        string url = BuildUrl("/v1/text/edit");

        var payload = new
        {
            text,
            mode = mode.ToString().ToLowerInvariant(),
            tone = mode == EditingMode.Tone ? tone.ToString().ToLowerInvariant() : null
        };

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Backend error ({response.StatusCode}): {error}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("output_text", out var output))
        {
            throw new InvalidOperationException("Backend response missing 'output_text'.");
        }

        return output.GetString() ?? string.Empty;
    }

    public async Task<BackendRuntimeConfig> GetRuntimeConfigAsync(
        string? overrideBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        string url = BuildUrl("/runtime/config", overrideBaseUrl);
        var config = await _httpClient.GetFromJsonAsync<BackendRuntimeConfig>(url, cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("Backend returned empty runtime config.");
        }

        return config;
    }

    public async Task<BackendRuntimeConfig> UpdateRuntimeConfigAsync(
        BackendRuntimeConfigUpdate update,
        string? overrideBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        string url = BuildUrl("/runtime/config", overrideBaseUrl);
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, update, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to update runtime config ({response.StatusCode}): {error}");
        }

        var config = await response.Content.ReadFromJsonAsync<BackendRuntimeConfig>(cancellationToken: cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("Backend returned empty runtime config.");
        }

        return config;
    }

    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        string? overrideBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        string url = BuildUrl("/runtime/models", overrideBaseUrl);
        var payload = await _httpClient.GetFromJsonAsync<ModelListResponse>(url, cancellationToken);
        if (payload == null)
        {
            throw new InvalidOperationException("Backend returned empty model list.");
        }

        return payload.Models;
    }

    public async Task<CheckResponse> CheckTextAsync(
        string text,
        LanguageToolSettings? languageToolConfig = null,
        CancellationToken cancellationToken = default)
    {
        string url = BuildUrl("/v1/text/check");

        object payload;
        if (languageToolConfig != null)
        {
            var disabledCategories = languageToolConfig.GetDisabledCategories();
            payload = disabledCategories.Count > 0
                ? new
                {
                    text,
                    language_tool_config = new { disabled_categories = disabledCategories }
                }
                : new { text } as object;
        }
        else
        {
            payload = new { text };
        }

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Gracefully handle errors for now, maybe return empty?
            // Or throw so caller knows? Throwing is safer for debugging.
            string error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Check failed ({response.StatusCode}): {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<CheckResponse>(cancellationToken: cancellationToken);
        return result ?? new CheckResponse();
    }

    public void Dispose() => _httpClient.Dispose();
}
