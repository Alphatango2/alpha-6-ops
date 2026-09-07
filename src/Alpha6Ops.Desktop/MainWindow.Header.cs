using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Alpha6Ops.Desktop;

public partial class MainWindow
{
    private LocalWeather? localWeather;
    private bool weatherRefreshFailed;
    private bool weatherDiagnostic;

    private void InitializeLocalWeather(bool diagnostic)
    {
        weatherDiagnostic = diagnostic;
        RenderLocalWeather(DateTimeOffset.UtcNow);
        if (!diagnostic) _ = RunLocalWeatherAsync();
    }

    private async Task RunLocalWeatherAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        var service = new LocalWeatherService(client);
        var token = lifetime.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                localWeather = await service.FetchAsync(token);
                weatherRefreshFailed = false;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
            {
                weatherRefreshFailed = true;
            }
            if (token.IsCancellationRequested) return;
            RenderLocalWeather(DateTimeOffset.UtcNow);
            try { await Task.Delay(TimeSpan.FromMinutes(weatherRefreshFailed ? 5 : 15), token); }
            catch (OperationCanceledException) { return; }
        }
    }

    internal void RenderClocks(DateTimeOffset now, TimeZoneInfo zone)
    {
        var utc = now.ToUniversalTime();
        var local = TimeZoneInfo.ConvertTime(now, zone);
        ClockText.Text = utc.ToString("HH:mm:ss 'Z'", CultureInfo.InvariantCulture);
        ClockDateText.Text = utc.ToString("dd MMM yyyy '· ZULU'", CultureInfo.InvariantCulture).ToUpperInvariant();
        LocalClockText.Text = local.ToString("HH:mm:ss 'LOCAL'", CultureInfo.InvariantCulture);
        LocalClockDateText.Text = local.ToString("dd MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        LocalClockText.ToolTip = $"Windows local time · {zone.DisplayName}\n{local:yyyy-MM-dd HH:mm:ss zzz}";
    }

    internal void SetHeaderWeather(LocalWeather? weather, bool refreshFailed = false)
    {
        localWeather = weather;
        weatherRefreshFailed = refreshFailed;
    }

    internal void RenderLocalWeather(DateTimeOffset now)
    {
        if (localWeather is null)
        {
            WeatherIcon.Code = -1;
            WeatherIcon.Opacity = 1;
            WeatherLocationText.Text = "LOCAL WEATHER";
            WeatherTemperatureText.Text = "—";
            WeatherConditionText.Text = weatherDiagnostic ? "OFFLINE TEST" : weatherRefreshFailed ? "UNAVAILABLE" : "LOCATING…";
            LocalWeatherButton.ToolTip = weatherDiagnostic ? "Network requests are disabled in diagnostic mode." :
                "Approximate city from your internet connection (ipwho.is); weather by Open-Meteo.\n" +
                (weatherRefreshFailed ? "Could not retrieve location or weather. Retrying in five minutes." : "Looking up your city and current weather…") +
                "\nA VPN may change the detected city. Local time uses the Windows time zone.";
            return;
        }
        var stale = weatherRefreshFailed || now - localWeather.ObservedAt > TimeSpan.FromMinutes(45);
        WeatherIcon.Code = localWeather.Code;
        WeatherIcon.Opacity = stale ? .55 : 1;
        WeatherLocationText.Text = localWeather.City.ToUpperInvariant();
        WeatherTemperatureText.Text = localWeather.Temperature(now.ToUnixTimeSeconds() / 5 % 2 == 0);
        WeatherConditionText.Text = stale ? "STALE · " + localWeather.Condition : localWeather.Condition;
        LocalWeatherButton.ToolTip = $"{localWeather.City} · approximate IP location (ipwho.is)\n" +
            $"{localWeather.Temperature(true)} / {localWeather.Temperature(false)} · {localWeather.Condition}\n" +
            $"Weather time: {localWeather.ObservedAt:dd MMM yyyy HH:mm}Z\n" +
            (stale ? "Last available weather; awaiting a fresh update.\n" : "Refreshes every 15 minutes; units switch every five seconds.\n") +
            "Source: Open-Meteo (open-meteo.com), current weather model data.\n" +
            "A VPN may change the detected city. Local time uses the Windows time zone.";
    }

    private void LocalWeather_Click(object sender, RoutedEventArgs e) =>
        OpsNoticeWindow.Show(this, "Local weather", LocalWeatherButton.ToolTip.ToString()!);
}
