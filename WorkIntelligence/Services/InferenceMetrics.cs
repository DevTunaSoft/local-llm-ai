namespace WorkIntelligence.Services;

public sealed record CompletionUsage(int PromptTokens,int CompletionTokens,double? PromptMilliseconds=null,double? GenerationMilliseconds=null);
public sealed record InferenceMetrics(CompletionUsage? Usage,double ElapsedMilliseconds,double? LoadMilliseconds,ulong PeakResidentBytes)
{
    public double? EndToEndTokensPerSecond=>Usage is not null && ElapsedMilliseconds>0?Usage.CompletionTokens/(ElapsedMilliseconds/1000):null;
    public double? GenerationTokensPerSecond=>Usage?.GenerationMilliseconds is >0?Usage.CompletionTokens/(Usage.GenerationMilliseconds.Value/1000):null;
}
