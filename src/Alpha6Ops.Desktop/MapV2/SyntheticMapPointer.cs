using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace Alpha6Ops.Desktop.MapV2;

// Diagnostic input only. Feed a coordinate-bearing move through WPF's input
// staging pipeline, as MouseDevice.Synchronize does, before raising a press.
// Neither the OS cursor nor private WebView2 controller state is modified.
// Fail closed if WPF changes this test-only input-report seam.
internal static class SyntheticMapPointer
{
    internal static void Move(FrameworkElement view,Point point)
    {
        var source=PresentationSource.FromVisual(view)??throw new InvalidOperationException("Synthetic pointer requires a presentation source");
        var client=source.CompositionTarget.TransformToDevice.Transform(view.TranslatePoint(point,(UIElement)source.RootVisual));
        var assembly=typeof(MouseDevice).Assembly;
        var reportType=assembly.GetType("System.Windows.Input.RawMouseInputReport",true)!;
        var actions=assembly.GetType("System.Windows.Input.RawMouseActions",true)!;
        var report=Activator.CreateInstance(reportType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
            [InputMode.Foreground,Environment.TickCount,source,Enum.Parse(actions,"AbsoluteMove, Activate"),(int)client.X,(int)client.Y,0,IntPtr.Zero],null)!;
        var argsType=assembly.GetType("System.Windows.Input.InputReportEventArgs",true)!;
        var args=(InputEventArgs)Activator.CreateInstance(argsType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[Mouse.PrimaryDevice,report],null)!;
        args.RoutedEvent=(RoutedEvent)(typeof(InputManager).GetField("PreviewInputReportEvent",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null)
            ??throw new InvalidOperationException("WPF diagnostic input-report event unavailable"));
        InputManager.Current.ProcessInput(args);
    }
}
