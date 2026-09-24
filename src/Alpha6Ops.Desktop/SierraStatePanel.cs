using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

public enum SierraStateKind { Loading, Empty, Unavailable, Stale, Error }

// Presentation only. Callers supply truthful state and optionally a retry/action control.
public sealed class SierraStatePanel : ContentControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(SierraStateKind), typeof(SierraStatePanel), new PropertyMetadata(SierraStateKind.Empty));
    public static readonly DependencyProperty HeadingProperty = DependencyProperty.Register(nameof(Heading), typeof(string), typeof(SierraStatePanel), new PropertyMetadata("Nothing to show"));
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(SierraStatePanel), new PropertyMetadata(""));
    public SierraStateKind Kind { get => (SierraStateKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Heading { get => (string)GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
}
