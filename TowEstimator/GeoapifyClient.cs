using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TowEstimator;

public sealed class GeoapifyClient
{
    private const string EncryptedApiKey = "NKXrQr50f3JUkzz2auQFuSf5kCHjXeEQ8Jv1FOMD+7TSAHc1WAdXv0eqEj9QNmWP";
    private const string CountryFilter = "us";
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeoapifyClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = CryptoUtil.DecryptBase64(EncryptedApiKey);
    }

    public async Task<IReadOnlyList<GeoPoint>> AutocompleteAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<GeoPoint>();
        }

        var url = BuildUri("https://api.geoapify.com/v1/geocode/autocomplete", text, 8);
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<GeoPoint>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParseFeatures(document);
    }

    public async Task<GeoPoint?> GeocodeFirstAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var url = BuildUri("https://api.geoapify.com/v1/geocode/search", text, 1);
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var items = ParseFeatures(document);
        return items.Count > 0 ? items[0] : null;
    }

    public async Task<double> RouteMilesAsync(GeoPoint start, GeoPoint end, CancellationToken cancellationToken)
    {
        var waypoints = string.Create(CultureInfo.InvariantCulture, $"{start.Latitude},{start.Longitude}|{end.Latitude},{end.Longitude}");
        var url = new UriBuilder("https://api.geoapify.com/v1/routing");
        url.Query = string.Join("&", new[]
        {
            $"waypoints={Uri.EscapeDataString(waypoints)}",
            "mode=drive",
            $"apiKey={Uri.EscapeDataString(_apiKey)}"
        });

        using var response = await _httpClient.GetAsync(url.Uri, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Routing error: {response.StatusCode}");
        }

        using var document = JsonDocument.Parse(json);
        var meters = document.RootElement
            .GetProperty("features")[0]
            .GetProperty("properties")
            .GetProperty("distance")
            .GetDouble();

        return meters / 1609.34d;
    }

    private static IReadOnlyList<GeoPoint> ParseFeatures(JsonDocument document)
    {
        var list = new List<GeoPoint>();
        if (!document.RootElement.TryGetProperty("features", out var features))
        {
            return list;
        }

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            var formatted = properties.GetProperty("formatted").GetString() ?? string.Empty;
            var lat = properties.GetProperty("lat").GetDouble();
            var lon = properties.GetProperty("lon").GetDouble();
            list.Add(new GeoPoint { Formatted = formatted, Latitude = lat, Longitude = lon });
        }

        return list;
    }

    private Uri BuildUri(string baseUrl, string text, int limit)
    {
        var uri = new UriBuilder(baseUrl);
        var query = new List<string>
        {
            $"text={Uri.EscapeDataString(text)}",
            $"apiKey={Uri.EscapeDataString(_apiKey)}",
            $"limit={limit}"
        };
        if (!string.IsNullOrWhiteSpace(CountryFilter))
        {
            query.Add($"filter=countrycode:{CountryFilter}");
        }

        uri.Query = string.Join("&", query);
        return uri.Uri;
    }
}
