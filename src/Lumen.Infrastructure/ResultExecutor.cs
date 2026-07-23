using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Lumen.Core;
namespace Lumen.Infrastructure;
public sealed class ResultExecutor(IApplicationStore store, ILogger<ResultExecutor> logger) : IResultExecutor
{
 public Task ExecuteAsync(SearchResult result,CancellationToken ct=default)
 {
  _=Task.Run(async()=>
  {
   try { Launch(result); await store.RecordUsageAsync(result,CancellationToken.None); }
   catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or FileNotFoundException or UnauthorizedAccessException) { logger.LogError(ex,"Failed to execute result {ResultId}",result.Id); }
  });
  return Task.CompletedTask;
 }
 private static void Launch(SearchResult result)
 {
  if(result.PrimaryAction.Kind=="url") { var chrome=FindChrome(); if(chrome is not null && !string.IsNullOrWhiteSpace(result.PrimaryAction.Arguments)) Process.Start(new ProcessStartInfo(chrome,$"--profile-directory=\"{result.PrimaryAction.Arguments}\" \"{result.PrimaryAction.Target}\""){UseShellExecute=true}); else Process.Start(new ProcessStartInfo(result.PrimaryAction.Target){UseShellExecute=true}); }
  else if(result.PrimaryAction.Kind=="appsfolder") Process.Start(new ProcessStartInfo("explorer.exe",$"shell:AppsFolder\\{result.PrimaryAction.Target}"){UseShellExecute=true});
  else if(result.PrimaryAction.Kind=="process") Process.Start(new ProcessStartInfo { FileName=result.PrimaryAction.Target, Arguments=result.PrimaryAction.Arguments??string.Empty, WorkingDirectory=result.PrimaryAction.WorkingDirectory??Path.GetDirectoryName(result.PrimaryAction.Target)!, UseShellExecute=true });
 }
 private static string? FindChrome()=>new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Google","Chrome","Application","chrome.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Google","Chrome","Application","chrome.exe")}.FirstOrDefault(File.Exists);
}
