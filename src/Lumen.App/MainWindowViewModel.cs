using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Lumen.Core;
namespace Lumen.App;
public sealed record SearchResultViewModel(SearchResult Result, System.Windows.Media.ImageSource? Icon)
{
 public string Title=>Result.Title;
 public string? Subtitle=>Result.Subtitle;
 public string TypeLabel=>Result.Type switch { SearchResultType.Application=>"应用",SearchResultType.PortableApplication=>"便携应用",SearchResultType.Quicklink=>"Quicklink",SearchResultType.Command=>"命令",_=>"结果"};
 public string IconGlyph=>Result.Type switch { SearchResultType.Application=>"◫",SearchResultType.PortableApplication=>"◈",SearchResultType.Quicklink=>"↗",SearchResultType.Command=>"›",_=>"·"};
}
public partial class MainWindowViewModel : ObservableObject
{
 private readonly SearchAggregator _search;private readonly IResultExecutor _executor;private readonly IconCacheService _icons;private CancellationTokenSource? _searchCts;private int _version;
 public ObservableCollection<SearchResultViewModel> Results {get;}=[];
 [ObservableProperty] private string query=string.Empty;
 [ObservableProperty] private bool isComposing;
 [ObservableProperty] private SearchResultViewModel? selectedResult;
 public MainWindowViewModel(SearchAggregator search,IResultExecutor executor,IconCacheService icons){_search=search;_executor=executor;_icons=icons;}
 partial void OnQueryChanged(string value)=>_=SearchAsync(value);
 private async Task SearchAsync(string text){var mine=Interlocked.Increment(ref _version);_searchCts?.Cancel();var cts=_searchCts=new CancellationTokenSource();try{await Task.Delay(75,cts.Token);var data=await _search.SearchAsync(new(text),cts.Token);if(mine!=_version)return;await System.Windows.Application.Current.Dispatcher.InvokeAsync(()=>{if(mine!=_version)return;Results.Clear();foreach(var r in data)Results.Add(new(r,_icons.GetIcon(r.IconKey)));SelectedResult=Results.FirstOrDefault();});}catch(OperationCanceledException){}}
 public void ClearTransientResults(){Interlocked.Increment(ref _version);_searchCts?.Cancel();Results.Clear();SelectedResult=null;}
 public async Task ExecuteSelectedAsync(){if(SelectedResult is null)return;await _executor.ExecuteAsync(SelectedResult.Result);Query=string.Empty;System.Windows.Application.Current.MainWindow?.Hide();}
}
