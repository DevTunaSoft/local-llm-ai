using System.Text.Json;

namespace WorkIntelligence.Services;

/// <summary>Persists CPU inference settings per trusted GGUF hash in the local data folder.</summary>
public static class ModelSettingsStore
{
    private static readonly object Gate = new();
    private static string FileName => Path.Combine(LocalPaths.Data, "model-settings.json");
    private static string Key(string sha256)
    {
        if (sha256 is null || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("Model settings require a valid SHA-256 key.", nameof(sha256));
        return sha256.ToUpperInvariant();
    }

    public static ModelRuntimeOptions Load(string sha256)
    {
        var key = Key(sha256);
        lock (Gate)
        {
            using var processLock = FileOperationLock.Acquire("settings:model-preferences");
            LocalPaths.Ensure();
            PathSafety.RejectReparsePoints(FileName);
            if (!File.Exists(FileName)) return new ModelRuntimeOptions();
            if (new FileInfo(FileName).Length > 262144)
                throw new InvalidDataException("Model settings file exceeds safety limit.");
            var saved = JsonSerializer.Deserialize<Dictionary<string, ModelRuntimeOptions>>(File.ReadAllText(FileName))
                ?? throw new InvalidDataException("Model settings JSON is invalid.");
            var options = saved.TryGetValue(key, out var found) ? found : new ModelRuntimeOptions();
            if (options is null) throw new InvalidDataException("Saved model options cannot be null.");
            options.Validate();
            return options;
        }
    }

    public static void Save(string sha256, ModelRuntimeOptions options)
    {
        var key = Key(sha256);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        lock (Gate)
        {
            LocalPaths.Ensure();
            PathSafety.RejectReparsePoints(FileName);
            Dictionary<string, ModelRuntimeOptions> saved;
            if (File.Exists(FileName))
            {
                if (new FileInfo(FileName).Length > 262144)
                    throw new InvalidDataException("Model settings file exceeds safety limit.");
                saved = JsonSerializer.Deserialize<Dictionary<string, ModelRuntimeOptions>>(File.ReadAllText(FileName))
                    ?? throw new InvalidDataException("Model settings JSON is invalid.");
            }
            else saved = new Dictionary<string, ModelRuntimeOptions>(StringComparer.Ordinal);
            saved[key] = options;
            var data = JsonSerializer.Serialize(saved);
            if (data.Length > 262144) throw new InvalidDataException("Too many saved model settings.");
            var temp = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, data);
                File.Move(temp, FileName, overwrite: true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
