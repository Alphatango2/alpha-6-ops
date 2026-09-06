using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Alpha6Ops.Desktop;

internal static class HeaderSmokeTest
{
    internal static async Task RunAsync(MainWindow window, Action<bool, string> check)
    {
        window.SetAdvanced(true);
        window.RestoreDashboardPreferences(new UserPreferences(1536, 1024, false, true, EmbeddedReplay.Fixtures[0], "Saved pilot"));
        check(window.AdvancedPanel.Visibility == Visibility.Collapsed,
            "Startup ignores legacy Advanced=true and keeps rotation details closed");
        check(window.PilotNameBox.Text == "Saved pilot" && (string)window.FixtureCombo.SelectedItem == EmbeddedReplay.Fixtures[0],
            "Closing rotation details on startup preserves the other saved dashboard preferences");
        window.RestoreDashboardPreferences(null);
        window.SetAdvanced(true);
        check(window.AdvancedPanel.Visibility == Visibility.Visible,
            "Rotation details can still be explicitly opened from Flight tools");
        window.SetAdvanced(false);
        check(window.CanQuickConnect && window.ConnectionBadge.IsEnabled && window.ConnectionBadge.Focusable,
            "Disconnected header is an enabled keyboard-focusable quick-connect button");
        check(AutomationProperties.GetName(window.ConnectionBadge) == "Simulator connection",
            "Quick connect exposes a descriptive accessible name");
        foreach (var button in new[] { window.ConnectionBadge, window.HeaderDispatchButton, window.LocalWeatherButton })
        {
            check(button.Focus() && button.IsKeyboardFocused, $"{button.Name} remains focusable without starting its action");
            window.UpdateLayout();
            var chrome = (Border)button.Template.FindName("Chrome", button);
            check(chrome.BorderThickness == button.BorderThickness &&
                (chrome.BorderBrush as SolidColorBrush)?.Color != Colors.White,
                $"{button.Name} does not retain a white or enlarged border while focused");
            check(ReferenceEquals(button.FocusVisualStyle, window.FindResource("OpsKeyboardFocus")),
                $"{button.Name} uses the shared keyboard-only focus visual");
        }
        var originalBackground = window.ConnectionBadge.Background;
        var originalDot = window.ConnectionDot.Fill;
        try
        {
            window.ConnectButton.IsEnabled = false; // Mirror the existing session/replay control guard without contacting MSFS.
            foreach (var (label, expected) in new[] {
                ("CONNECTING TO SIMULATOR", "CONNECTING"), ("SIMULATOR CONNECTED", "CONNECTED"),
                ("SIMULATOR CONNECTION LOST — RETRYING", "RECONNECTING") })
            {
                window.SetConnectionBadge(label, "#FFCA45", "#433817");
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                check(!window.CanQuickConnect && window.ConnectionActionText.Text == "VIEW CONTROLS" && window.ConnectionBadgeText.Text == expected,
                    $"Header offers controls, not a duplicate connection, while {expected}");
                window.ToolsOverlay.Visibility = Visibility.Collapsed;
                window.SetAdvanced(true);
                window.ConnectionBadge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(window.ToolsOverlay.Visibility == Visibility.Visible && !window.ConnectButton.IsEnabled,
                    $"Clicking {expected} opens controls without changing the connection state");
                check(window.AdvancedPanel.Visibility == Visibility.Collapsed,
                    $"Quick connect closes an already-open rotation panel while {expected}");
            }
            window.SetConnectionBadge("SIMULATOR CONNECTION FAILED", "#FF9690", "#512621");
            window.ConnectButton.IsEnabled = true;
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            check(window.CanQuickConnect && window.ConnectionActionText.Text == "CLICK TO CONNECT" && window.ConnectionBadgeText.Text == "FAILED",
                "Failed connection offers quick retry and retains the full error status in its tooltip");
            var failureBrush = (SolidColorBrush)window.ConnectionBadge.Background;
            var neutral = (Color)ColorConverter.ConvertFromString("#091219");
            var red = (Color)ColorConverter.ConvertFromString("#FF9690");
            check(failureBrush.HasAnimatedProperties && (Color)failureBrush.GetAnimationBaseValue(SolidColorBrush.ColorProperty) == neutral,
                "Connection failure starts a single flash over a neutral resting background");
            await Task.Delay(850);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            check(failureBrush.Color == neutral && ((SolidColorBrush)window.ConnectionDot.Fill).Color == red && window.CanQuickConnect,
                "After the failure flash only the indicator stays red and quick retry remains available");
            window.SetConnectionBadge("SIMULATOR CONNECTION FAILED", "#FF9690", "#512621");
            check(((SolidColorBrush)window.ConnectionBadge.Background).HasAnimatedProperties && window.CanQuickConnect,
                "A repeated failure restarts the brief flash without blocking retry");
            window.SetConnectionBadge("CONNECTING TO SIMULATOR", "#FFCA45", "#433817");
            var connectingBrush = window.ConnectionBadge.Background;
            await Task.Delay(850);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            check(ReferenceEquals(window.ConnectionBadge.Background, connectingBrush) &&
                ((SolidColorBrush)connectingBrush).Color == (Color)ColorConverter.ConvertFromString("#433817") &&
                ((SolidColorBrush)window.ConnectionDot.Fill).Color == (Color)ColorConverter.ConvertFromString("#FFCA45") &&
                window.ConnectionBadgeText.Text == "CONNECTING",
                "Retry replaces the failure flash immediately and keeps the new connection state");
        }
        finally
        {
            window.ConnectButton.IsEnabled = true;
            window.ConnectionBadgeText.Text = "DISCONNECTED";
            window.ConnectionBadgeText.ToolTip = null;
            window.ConnectionBadge.Background = originalBackground;
            window.ConnectionDot.Fill = originalDot;
            window.ToolsOverlay.Visibility = Visibility.Collapsed;
        }
        var now = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        window.RenderClocks(now, zone);
        check(window.ClockText.Text == "00:00:00 Z" && window.LocalClockText.Text == "19:00:00 LOCAL",
            "Zulu and local clocks include seconds and apply daylight saving time");
        check(window.ClockDateText.Text.StartsWith("06 SEP", StringComparison.Ordinal) && window.LocalClockDateText.Text.StartsWith("05 SEP", StringComparison.Ordinal),
            "Each clock shows its own date across UTC midnight");
        window.RenderClocks(new DateTimeOffset(2026, 11, 1, 7, 0, 1, TimeSpan.Zero), zone);
        check(window.LocalClockText.Text == "01:00:01 LOCAL", "Local clock applies the daylight saving fall-back transition");
        var reading = new LocalWeather("Milwaukee", 25, 3, now);
        window.SetHeaderWeather(reading);
        window.RenderLocalWeather(now);
        check(window.WeatherTemperatureText.Text == "77°F", "Header converts Celsius to Fahrenheit");
        check(window.WeatherIcon.Code == 3, "Condition icon follows the same weather reading as the temperature");
        foreach (var (code, kind) in new[] { (0, "sun"), (2, "partly-cloudy"), (3, "cloud"), (45, "fog"), (61, "rain"), (71, "snow"), (95, "storm"), (-1, "unknown") })
            check(WeatherConditionIcon.Kind(code) == kind, $"Weather code {code} maps to a {kind} icon");
        window.RenderLocalWeather(now.AddSeconds(5));
        check(window.WeatherTemperatureText.Text == "25°C", "Temperature switches to Celsius after five seconds");
        window.RenderLocalWeather(now.AddSeconds(10));
        check(window.WeatherTemperatureText.Text == "77°F", "Temperature cycles back to Fahrenheit");
        check(new LocalWeather("Test", -40, 0, now).Temperature(true) == "-40°F", "Negative temperature conversion is correct");
        window.RenderLocalWeather(now.AddHours(1));
        check(window.WeatherConditionText.Text.StartsWith("STALE", StringComparison.Ordinal), "Old model weather is labeled stale");
        window.SetHeaderWeather(reading, true);
        window.RenderLocalWeather(now);
        check(window.WeatherConditionText.Text.StartsWith("STALE", StringComparison.Ordinal), "Refresh failure marks the retained reading stale");
        window.SetHeaderWeather(null, true);
        window.RenderLocalWeather(now);
        check(window.WeatherTemperatureText.Text == "—", "Unavailable weather never displays a fabricated temperature");
        check(window.WeatherIcon.Code == -1, "Unavailable weather uses a neutral icon instead of suggesting clear skies");

        const string location = """{"success":true,"city":"Milwaukee","latitude":43.0389,"longitude":-87.9067}""";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var weather = JsonSerializer.Serialize(new { current = new { temperature_2m = 25, weather_code = 3, time = timestamp } });
        using var handler = new FixtureHandler(location, weather);
        using var client = new HttpClient(handler);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var fetched = await new LocalWeatherService(client).FetchAsync(CancellationToken.None);
            check(fetched.City == "Milwaukee" && fetched.Celsius == 25 && fetched.Code == 3 && fetched.ObservedAt.ToUnixTimeSeconds() == timestamp,
                "Weather service parses location and current model weather through HTTP");
            check(handler.Requests.Count == 2 && handler.Requests[1].Contains("latitude=43.039&longitude=-87.907", StringComparison.Ordinal),
                "Weather query uses approximate coordinates and invariant decimal formatting");
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }

        foreach (var invalid in new[] { "null", "{}", "{\"current\":null}", "{\"current\":{\"temperature_2m\":null}}" })
        {
            using var badHandler = new FixtureHandler(location, invalid);
            using var badClient = new HttpClient(badHandler);
            var rejected = false;
            try { await new LocalWeatherService(badClient).FetchAsync(CancellationToken.None); }
            catch (JsonException) { rejected = true; }
            check(rejected, "Weather service rejects malformed or missing readings: " + invalid);
        }
        using var deniedHandler = new FixtureHandler("{\"success\":false}");
        using var deniedClient = new HttpClient(deniedHandler);
        var denied = false;
        try { await new LocalWeatherService(deniedClient).FetchAsync(CancellationToken.None); }
        catch (JsonException) { denied = true; }
        check(denied && deniedHandler.Requests.Count == 1, "Failed location lookup does not request weather for invented coordinates");
        using var unavailableHandler = new FixtureHandler(location) { Status = HttpStatusCode.TooManyRequests };
        using var unavailableClient = new HttpClient(unavailableHandler);
        var unavailable = false;
        try { await new LocalWeatherService(unavailableClient).FetchAsync(CancellationToken.None); }
        catch (HttpRequestException) { unavailable = true; }
        check(unavailable, "Provider rate limits are surfaced as recoverable HTTP failures");

        window.SetHeaderWeather(new LocalWeather("Milwaukee", 25, 3, DateTimeOffset.UtcNow));
        window.RenderClocks(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        window.RenderLocalWeather(DateTimeOffset.UtcNow);
    }

    private sealed class FixtureHandler(params string[] responses) : HttpMessageHandler
    {
        internal List<string> Requests { get; } = [];
        internal HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(responses[Requests.Count - 1]) });
        }
    }
}
