using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha6Ops.Desktop;

internal sealed record LocalWeather(string City, double Celsius, int Code, DateTimeOffset ObservedAt)
{
    internal string Temperature(bool fahrenheit) =>
        (fahrenheit ? Celsius * 9 / 5 + 32 : Celsius).ToString("0", CultureInfo.CurrentCulture) + (fahrenheit ? "°F" : "°C");

    internal string Condition => Code switch
    {
        0 => "CLEAR", 1 => "MOSTLY CLEAR", 2 => "PARTLY CLOUDY", 3 => "OVERCAST",
        45 or 48 => "FOG", 51 or 53 or 55 => "DRIZZLE", 56 or 57 => "FREEZING DRIZZLE",
        61 or 63 or 65 => "RAIN", 66 or 67 => "FREEZING RAIN", 71 or 73 or 75 or 77 => "SNOW",
        80 or 81 or 82 => "RAIN SHOWERS", 85 or 86 => "SNOW SHOWERS",
        95 or 96 or 99 => "THUNDERSTORM", _ => "CONDITIONS UNKNOWN"
    };
}

// No device/GPS access: request only city-level IP location, then current model weather.
// The HttpClient is supplied so the real HTTP/parsing path can be verified offline.
internal sealed class LocalWeatherService(HttpClient client)
{
    internal async Task<LocalWeather> FetchAsync(CancellationToken cancellationToken)
    {
        using var locationResponse = await client.GetAsync(
            "https://ipwho.is/?fields=success,city,region,country,latitude,longitude", cancellationToken);
        locationResponse.EnsureSuccessStatusCode();
        using var locationJson = JsonDocument.Parse(await locationResponse.Content.ReadAsStringAsync(cancellationToken));
        var location = locationJson.RootElement;
        if (location.ValueKind != JsonValueKind.Object || !location.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
            throw new JsonException("Location lookup did not succeed.");
        var latitude = RequiredNumber(location, "latitude");
        var longitude = RequiredNumber(location, "longitude");
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new JsonException("Invalid location coordinates.");
        var city = OptionalText(location, "city") ?? OptionalText(location, "region") ?? OptionalText(location, "country")
            ?? throw new JsonException("Location name is missing.");
        var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude:F3}&longitude={longitude:F3}&current=temperature_2m,weather_code&temperature_unit=celsius&timeformat=unixtime&timezone=UTC&forecast_days=1");
        using var weatherResponse = await client.GetAsync(url, cancellationToken);
        weatherResponse.EnsureSuccessStatusCode();
        using var weatherJson = JsonDocument.Parse(await weatherResponse.Content.ReadAsStringAsync(cancellationToken));
        if (weatherJson.RootElement.ValueKind != JsonValueKind.Object || !weatherJson.RootElement.TryGetProperty("current", out var current))
            throw new JsonException("Current weather is missing.");
        var temperature = RequiredNumber(current, "temperature_2m");
        var code = RequiredNumber(current, "weather_code");
        var seconds = RequiredNumber(current, "time");
        if (temperature is < -100 or > 70 || code is < 0 or > 99 || code != Math.Truncate(code) ||
            seconds is < 0 or > 253402300799)
            throw new JsonException("Current weather values are invalid.");
        var observedAt = DateTimeOffset.FromUnixTimeSeconds((long)seconds);
        if (observedAt > DateTimeOffset.UtcNow.AddMinutes(15))
            throw new JsonException("Weather timestamp is in the future.");
        return new LocalWeather(city, temperature, (int)code, observedAt);
    }

    private static double RequiredNumber(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new JsonException($"Missing or invalid {name}.");
        return number;
    }

    private static string? OptionalText(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!.Trim() : null;
}
