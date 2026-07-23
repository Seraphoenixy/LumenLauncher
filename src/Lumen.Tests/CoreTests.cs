using Lumen.Core;
using Xunit;
namespace Lumen.Tests;
public class CoreTests
{
 [Fact] public void CandidateScorer_PrefersProductExecutable(){var scorer=new DefaultExecutableCandidateScorer();var good=new ExecutableCandidate("D:\\Tools\\Foo\\foo.exe","foo.exe","Foo","Application",null,null,DateTimeOffset.Now,1,true,"Foo",1,false);var bad=good with { Path="D:\\Tools\\Foo\\uninstall.exe",FileName="uninstall.exe",ProductName=null,FileDescription=null};Assert.True(scorer.Score(good)>scorer.Score(bad));}
 [Fact] public void TextMatcher_Prefix_BeatsContains(){Assert.True(TextMatcher.Score("Visual Studio","vis")>TextMatcher.Score("My Visual Studio","vis"));}
 [Fact] public void TextMatcher_NoMatch_IsZero()=>Assert.Equal(0,TextMatcher.Score("Lumen","chrome"));
 [Fact] public void CandidateScore_LaunchHistory_IncreasesScore(){var scorer=new DefaultExecutableCandidateScorer();var c=new ExecutableCandidate("a.exe","a.exe",null,null,null,null,DateTimeOffset.Now,1,false,"a",1,false);Assert.Equal(40,scorer.Score(c with { WasLaunched=true})-scorer.Score(c));}
}
