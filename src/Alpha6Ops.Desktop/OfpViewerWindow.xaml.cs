using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using System.Text.Json;using System.Text.RegularExpressions;using System.Windows;using System.Windows.Controls;using System.Windows.Input;
namespace Alpha6Ops.Desktop;
internal sealed record OfpBookmark(string Title,FrameworkElement Target);
public partial class OfpViewerWindow:Window
{
 readonly string stateDirectory;
 readonly string content;readonly string? pdfPath;readonly string stateKey;readonly List<TextBlock> sections=[];double zoom=1;bool bookmarksVisible=true;string? activeBookmark;
 internal int BookmarkCount=>BookmarkItems.Items.Count;internal bool IsContinuousDocument=>DocumentContent.Children.Count>0;internal string? ActiveBookmark=>activeBookmark;
 internal OfpViewerWindow(ActiveFlightPlan plan, string? directory = null)
 {
  stateDirectory=directory??CrashReporter.RootDirectory;
  InitializeComponent();stateKey=plan.OfpCacheKey??$"{plan.FlightNumber}-{plan.PlannedDepartureUtc:yyyyMMddHHmm}";ReleaseText.Text=$"{plan.FlightNumber}  •  {plan.Origin} → {plan.Destination}  •  SIMBRIEF RELEASE";pdfPath=SimBriefImporter.OfpPdfPath(plan.OfpCacheKey, stateDirectory);OpenPdfButton.IsEnabled=pdfPath is not null;
  var textPath=SimBriefImporter.OfpTextPath(plan.OfpCacheKey, stateDirectory);content=textPath is not null?File.ReadAllText(textPath):"THE IN-APP OFP CONTENT IS NOT AVAILABLE FOR THIS RELEASE.\n\nRefresh the flight from SimBrief in Dispatch. If an original PDF was cached, use Open PDF Externally.";BuildDocument();DocumentStatusText.Text=textPath is null?"OFP CONTENT UNAVAILABLE":pdfPath is null?"CACHED OFP • ORIGINAL PDF UNAVAILABLE":"CACHED OFP • ORIGINAL PDF AVAILABLE";
  Loaded+=(_,_)=>RestoreState();Closed+=(_,_)=>SaveState();PreviewKeyDown+=Viewer_KeyDown;
 }
 void BuildDocument()
 {
  DocumentContent.Children.Clear();sections.Clear();var chunks=SplitSections(content);var bookmarks=new List<OfpBookmark>();
  foreach(var (title,text) in chunks){var block=new TextBlock{Text=text,FontFamily=new System.Windows.Media.FontFamily("Cascadia Mono, Consolas"),FontSize=14,LineHeight=20,Foreground=OpsUi.Brush("#E4EBF0"),TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(0,0,0,24)};DocumentContent.Children.Add(block);sections.Add(block);bookmarks.Add(new(title,block));}
  var end=new Border{Height=2,Background=OpsUi.Brush("#FFDA00"),Margin=new Thickness(0,2,0,20)};DocumentContent.Children.Add(end);bookmarks.Add(new("End of document",end));BookmarkItems.ItemsSource=bookmarks;ApplyZoom();
 }
 internal static IReadOnlyList<(string Title,string Text)> SplitSections(string text)
 {
  var markers=new[]{("ATC flight plan",@"ATC\s+(FLIGHT\s+)?PLAN"),("Additional information",@"ADDITIONAL\s+(INFORMATION|INFO)"),("Runway analysis",@"RUNWAY\s+ANALYSIS"),("Airport weather",@"(AIRPORT\s+WX|WEATHER\s+LIST|METAR)"),("NOTAMs",@"^\s*NOTAMS?\s*:?") ,("Company NOTAMs",@"COMPANY\s+NOTAMS?"),("Maps",@"^\s*MAPS?\s*:?")};
  var hits=new List<(int Index,string Title)>{(0,"OFP summary")};foreach(var marker in markers){var match=Regex.Match(text,marker.Item2,RegexOptions.IgnoreCase|RegexOptions.Multiline);if(match.Success&&match.Index>0)hits.Add((match.Index,marker.Item1));}hits=hits.OrderBy(x=>x.Index).GroupBy(x=>x.Index).Select(g=>g.First()).ToList();var result=new List<(string,string)>();for(var i=0;i<hits.Count;i++){var end=i+1<hits.Count?hits[i+1].Index:text.Length;result.Add((hits[i].Title,text[hits[i].Index..end].Trim()));}return result;
 }
 void Bookmark_Click(object sender,RoutedEventArgs e){if(((Button)sender).Tag is OfpBookmark bookmark){bookmark.Target.BringIntoView();SelectBookmark(bookmark);}}
 void SelectBookmark(OfpBookmark bookmark){activeBookmark=bookmark.Title;foreach(var item in BookmarkItems.Items.OfType<OfpBookmark>())if(BookmarkItems.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter presenter&&FindButton(presenter) is{} button){button.BorderBrush=item==bookmark?OpsUi.Brush("#FFDA00"):System.Windows.Media.Brushes.Transparent;button.Foreground=item==bookmark?OpsUi.Brush("#FFDA00"):OpsUi.Brush("#D7E1E8");}}
 static Button? FindButton(System.Windows.DependencyObject root){if(root is Button b)return b;for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)if(FindButton(System.Windows.Media.VisualTreeHelper.GetChild(root,i)) is{} found)return found;return null;}
 void ToggleBookmarks_Click(object sender,RoutedEventArgs e){bookmarksVisible=!bookmarksVisible;BookmarkPanel.Visibility=bookmarksVisible?Visibility.Visible:Visibility.Collapsed;BookmarkColumn.Width=new GridLength(bookmarksVisible?235:42);ShowBookmarksButton.Visibility=bookmarksVisible?Visibility.Collapsed:Visibility.Visible;}
 void DocumentScroll_ScrollChanged(object sender,ScrollChangedEventArgs e){if(!IsLoaded||e.VerticalChange==0)return;var entries=BookmarkItems.Items.OfType<OfpBookmark>().Where(item=>item.Target.IsVisible).Select(item=>(Item:item,Y:item.Target.TranslatePoint(new Point(0,0),DocumentContent).Y)).OrderBy(item=>item.Y).ToArray();var visible=entries.LastOrDefault(item=>item.Y<=DocumentScroll.VerticalOffset+80).Item??entries.FirstOrDefault().Item;if(visible is not null)SelectBookmark(visible);}
 void ApplyZoom(){foreach(var block in sections){block.FontSize=14*zoom;block.LineHeight=20*zoom;}ZoomText.Text=$"{zoom*100:0}%";}
 void ZoomOut_Click(object s,RoutedEventArgs e){zoom=Math.Max(.7,zoom-.1);ApplyZoom();}void ZoomIn_Click(object s,RoutedEventArgs e){zoom=Math.Min(2,zoom+.1);ApplyZoom();}void Fit_Click(object s,RoutedEventArgs e){zoom=1;ApplyZoom();DocumentScroll.ScrollToTop();}
 void OpenPdf_Click(object s,RoutedEventArgs e){if(pdfPath is not null)Process.Start(new ProcessStartInfo(pdfPath){UseShellExecute=true});}void Close_Click(object s,RoutedEventArgs e)=>Close();void Minimize_Click(object s,RoutedEventArgs e)=>WindowState=WindowState.Minimized;void Maximize_Click(object s,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;void TitleBar_MouseLeftButtonDown(object s,MouseButtonEventArgs e){if(e.ClickCount==2)Maximize_Click(s,e);else DragMove();}
 void Viewer_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.B&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){ToggleBookmarks_Click(this,e);e.Handled=true;}else if(e.Key is Key.Add or Key.OemPlus){ZoomIn_Click(this,e);e.Handled=true;}else if(e.Key is Key.Subtract or Key.OemMinus){ZoomOut_Click(this,e);e.Handled=true;}}
 string StatePath=>Path.Combine(stateDirectory,"ofp-viewer-state.json");
 void RestoreState(){try{if(!File.Exists(StatePath))return;var all=JsonSerializer.Deserialize<Dictionary<string,ViewerState>>(File.ReadAllText(StatePath));if(all is null||!all.TryGetValue(stateKey,out var state))return;zoom=Math.Clamp(state.Zoom,.7,2);if(state.BookmarksVisible!=bookmarksVisible)ToggleBookmarks_Click(this,new RoutedEventArgs());ApplyZoom();Dispatcher.BeginInvoke(()=>DocumentScroll.ScrollToVerticalOffset(state.Offset));}catch(Exception error)when(error is IOException or JsonException or UnauthorizedAccessException){}}
 void SaveState(){try{Directory.CreateDirectory(stateDirectory);Dictionary<string,ViewerState> all=[];if(File.Exists(StatePath))all=JsonSerializer.Deserialize<Dictionary<string,ViewerState>>(File.ReadAllText(StatePath))??[];all[stateKey]=new(zoom,DocumentScroll.VerticalOffset,bookmarksVisible);File.WriteAllText(StatePath,JsonSerializer.Serialize(all));}catch(Exception error)when(error is IOException or JsonException or UnauthorizedAccessException){}}
 internal sealed record ViewerState(double Zoom,double Offset,bool BookmarksVisible);
}
