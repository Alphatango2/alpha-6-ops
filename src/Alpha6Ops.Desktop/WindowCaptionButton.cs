using System.Windows;
using System.Windows.Controls;

namespace Alpha6Ops.Desktop;

internal enum WindowCaptionAction { Minimize, Maximize, Restore, Close }

internal sealed class WindowCaptionButton : Button
{
    internal WindowCaptionAction Action { get; set; }
    protected override void OnClick()
    {
        base.OnClick();
        ExecuteForTest();
    }
    internal void ExecuteForTest()
    {
        if(Window.GetWindow(this) is not { } window)return;
        switch(Action)
        {
            case WindowCaptionAction.Minimize:window.WindowState=WindowState.Minimized;break;
            case WindowCaptionAction.Maximize:window.WindowState=WindowState.Maximized;break;
            case WindowCaptionAction.Restore:window.WindowState=WindowState.Normal;break;
            case WindowCaptionAction.Close:window.Close();break;
        }
    }
}
