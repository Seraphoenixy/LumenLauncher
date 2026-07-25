using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Lumen.Core;
namespace Lumen.App;
public partial class MainWindow : Window, ILauncherWindowService
{
 private bool _exitRequested;
 private System.Windows.Point? _dragStart;
 private readonly ISettingsService _settings;
 public MainWindow(MainWindowViewModel vm,ISettingsService settings)
 {
  _settings=settings;InitializeComponent();RestorePosition();DataContext=vm;
  TextCompositionManager.AddTextInputStartHandler(SearchBox,(_,_)=>vm.IsComposing=true);TextCompositionManager.AddTextInputHandler(SearchBox,(_,_)=>vm.IsComposing=false);
  SearchBox.LostKeyboardFocus+=(_,_)=>vm.IsComposing=false;Loaded+=(_,_)=>FocusSearchBox();Activated+=(_,_)=>FocusSearchBox();Deactivated+=OnDeactivated;PreviewKeyDown+=OnKeyDown;Closing+=OnClosing;
 }
 public void ShowLauncher(){if(!IsVisible)Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();Topmost=true;FocusSearchBox();Dispatcher.BeginInvoke(FocusSearchBox,System.Windows.Threading.DispatcherPriority.Input);}
 public void HideLauncher(){if(DataContext is MainWindowViewModel vm){vm.Query=string.Empty;vm.ClearTransientResults();vm.IsComposing=false;}Hide();}
 public void ToggleLauncher(){if(IsVisible)HideLauncher();else ShowLauncher();}
 private void FocusSearchBox(){Focus();SearchBox.Focus();Keyboard.Focus(SearchBox);}
 public void CloseForShutdown(){_exitRequested=true;Close();}
 private void OnDeactivated(object? sender,EventArgs e){if(_settings.Current.Window.HideOnDeactivated)HideLauncher();}
 private void OnClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(_exitRequested)return;e.Cancel=true;HideLauncher();}
 private void BeginSearchDrag(object sender,MouseButtonEventArgs e){_dragStart=e.GetPosition(this);}
 private void ContinueSearchDrag(object sender,System.Windows.Input.MouseEventArgs e){if(_dragStart is not System.Windows.Point start||e.LeftButton!=MouseButtonState.Pressed)return;var current=e.GetPosition(this);if(Math.Abs(current.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(current.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;_dragStart=null;try{DragMove();_ = SavePositionAsync();}catch(InvalidOperationException){}}
 private void EndSearchDrag(object sender,MouseButtonEventArgs e)=>_dragStart=null;
 private void RestorePosition(){var window=_settings.Current.Window;if(window.Left is not double left||window.Top is not double top||double.IsNaN(left)||double.IsNaN(top))return;WindowStartupLocation=WindowStartupLocation.Manual;Left=left;Top=top;}
 private async Task SavePositionAsync(){if(WindowState!=WindowState.Normal)return;_settings.Current.Window.Left=Left;_settings.Current.Window.Top=Top;await _settings.SaveAsync();}
 private async void OnKeyDown(object sender,System.Windows.Input.KeyEventArgs e)
 {
  if(e.Key==Key.Escape){HideLauncher();return;}
  if(e.Key==Key.Down){MoveSelection(1);e.Handled=true;return;}
  if(e.Key==Key.Up){MoveSelection(-1);e.Handled=true;return;}
  if(e.Key==Key.Enter){await ((MainWindowViewModel)DataContext).ExecuteSelectedAsync();}
 }
 private void MoveSelection(int offset)
 {
  var vm=(MainWindowViewModel)DataContext;var count=vm.Results.Count;if(count==0)return;
  var current=vm.SelectedResult is null?-1:vm.Results.IndexOf(vm.SelectedResult);
  vm.SelectedResult=vm.Results[Math.Clamp(current+offset,0,count-1)];
  FindChild<System.Windows.Controls.ListBox>(this)?.ScrollIntoView(vm.SelectedResult);
 }
 private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
 {
  for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++){var child=VisualTreeHelper.GetChild(root,index);if(child is T result)return result;var nested=FindChild<T>(child);if(nested is not null)return nested;}
  return null;
 }
 private async void Execute(object sender,MouseButtonEventArgs e)
 {
  if(sender is not System.Windows.Controls.ListBox list||e.OriginalSource is not DependencyObject source||System.Windows.Controls.ItemsControl.ContainerFromElement(list,source) is not System.Windows.Controls.ListBoxItem item)return;
  list.SelectedItem=item.DataContext;
  await ((MainWindowViewModel)DataContext).ExecuteSelectedAsync();
 }
}
