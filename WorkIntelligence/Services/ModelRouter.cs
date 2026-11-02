using System.Text.Json;
using System.Text.RegularExpressions;
using WorkIntelligence.Models;

namespace WorkIntelligence.Services;

public enum WorkModelTask { General, Analysis, Code }
public sealed record RoutingPreferences(bool Enabled=false,string? GeneralModelSha=null,string? AnalysisModelSha=null,string? CodeModelSha=null);
public sealed record RoutingDecision(ModelRecord Model,WorkModelTask Task,string Reason);
internal sealed record RoutingCandidate(ModelRecord Model,ulong EstimatedBytes,bool Available=true);

public static class ModelRouter
{
    private static string FileName=>Path.Combine(LocalPaths.Data,"model-routing.json");
    public static RoutingPreferences Load()
    {
        PathSafety.RejectReparsePoints(FileName);
        if(!File.Exists(FileName))return new();
        if(new FileInfo(FileName).Length>16384)throw new InvalidDataException("Model routing settings are oversized.");
        var settings=JsonSerializer.Deserialize<RoutingPreferences>(File.ReadAllText(FileName)) ?? throw new InvalidDataException("Model routing settings are malformed.");
        Validate(settings);return settings;
    }
    public static void Save(RoutingPreferences preferences)
    {
        Validate(preferences);LocalPaths.Ensure();PathSafety.RejectReparsePoints(FileName);
        var temp=FileName+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonSerializer.Serialize(preferences));File.Move(temp,FileName,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    private static void Validate(RoutingPreferences preferences)
    {
        foreach(var hash in new[]{preferences.GeneralModelSha,preferences.AnalysisModelSha,preferences.CodeModelSha})
            if(hash is not null && (hash.Length!=64 || !hash.All(Uri.IsHexDigit)))throw new ArgumentException("Routing models require trusted SHA-256 identities.");
        if(preferences.Enabled && preferences.GeneralModelSha is null)throw new ArgumentException("Choose a general model before enabling automatic routing.");
    }
    public static WorkModelTask Classify(string prompt)
    {
        var words=Regex.Matches(prompt[..Math.Min(prompt.Length,8000)],@"[\p{L}\p{N}_]+")
            .Select(x=>x.Value.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        if(words.Overlaps(new[]{"code","debug","exception","stacktrace","compile","sql","script","powershell","csharp"}))return WorkModelTask.Code;
        if(words.Overlaps(new[]{"analyze","analyse","compare","investigate","reason","strategy","assess","tradeoffs","policy"}))return WorkModelTask.Analysis;
        return WorkModelTask.General;
    }
    public static RoutingDecision Choose(RoutingPreferences preferences,IReadOnlyList<ModelRecord> models,string prompt,
        ModelRecord? selected,bool manualOverride,ulong availableBytes,WorkModelTask? requestedTask=null)
    {
        Validate(preferences);
        var task=requestedTask ?? Classify(prompt);
        var candidates=new List<RoutingCandidate>();
        foreach(var model in models)
        {
            try
            {
                var metadata=GgufMetadata.Read(model.Path);
                var options=ModelSettingsStore.Load(model.Sha256);
                var needed=checked(HardwareProbe.EstimateModelBytes(new FileInfo(model.Path).Length)+metadata.EstimateCacheBytes(options.ContextSize));
                var withinUserBudget=options.MemoryLimitMiB is null || needed<=(ulong)options.MemoryLimitMiB.Value*1024*1024;
                candidates.Add(new(model,needed,withinUserBudget));
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or OverflowException)
            {candidates.Add(new(model,ulong.MaxValue,false));}
        }
        return Decide(preferences,candidates,task,selected,manualOverride,availableBytes);
    }
    internal static RoutingDecision Decide(RoutingPreferences preferences,IReadOnlyList<RoutingCandidate> candidates,
        WorkModelTask task,ModelRecord? selected,bool manualOverride,ulong availableBytes)
    {
        const ulong reserve=2UL*1024*1024*1024;
        var eligible=candidates.Where(x=>x.Available && availableBytes>=reserve && x.EstimatedBytes<=availableBytes-reserve).ToArray();
        if(manualOverride || !preferences.Enabled)
        {
            var manual=eligible.FirstOrDefault(x=>x.Model.Id==selected?.Id)
                ?? throw new InvalidOperationException("The manually selected model is unavailable or exceeds the physical/user memory budget.");
            return new(manual.Model,task,"Manual model selection overrides automatic routing; memory reserve checks passed.");
        }
        var preferred=task switch{WorkModelTask.Code=>preferences.CodeModelSha,WorkModelTask.Analysis=>preferences.AnalysisModelSha,_=>preferences.GeneralModelSha};
        RoutingCandidate? Find(string? sha)=>eligible.FirstOrDefault(x=>string.Equals(x.Model.Sha256,sha,StringComparison.OrdinalIgnoreCase));
        var match=Find(preferred);
        if(match is not null)return new(match.Model,task,$"{task} profile selected its configured model; estimated memory fits with a 2 GiB reserve.");
        var fallback=Find(preferences.GeneralModelSha);
        if(fallback is not null)return new(fallback.Model,task,$"The {task} preference is missing or exceeds its memory budget; using the configured general model.");
        var smallest=eligible.OrderBy(x=>x.EstimatedBytes).ThenBy(x=>x.Model.Id).FirstOrDefault()
            ?? throw new InvalidOperationException("No available registered model fits the current physical/user memory budget. Close applications, reduce context or import a smaller compatible model.");
        return new(smallest.Model,task,$"Configured models are unavailable or exceed their memory budgets; using the smallest eligible registered model ({smallest.Model.DisplayName}).");
    }
}
