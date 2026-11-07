using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ModelSettingsTests
{
    [Fact]
    public void PerModelPreferencesPersistAndStayIsolated()
    {
        const string first = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        const string second = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
        var firstOptions = new ModelRuntimeOptions(8192, 6, 768, 0.25);
        ModelSettingsStore.Save(first, firstOptions);
        Assert.Equal(firstOptions, ModelSettingsStore.Load(first.ToLowerInvariant()));
        Assert.Equal(new ModelRuntimeOptions(), ModelSettingsStore.Load(second));
        var secondOptions = new ModelRuntimeOptions(2048, 2, 256, 0.1);
        ModelSettingsStore.Save(second, secondOptions);
        Assert.Equal(firstOptions, ModelSettingsStore.Load(first));
        Assert.Equal(secondOptions, ModelSettingsStore.Load(second));
    }

    [Fact]
    public async Task ConcurrentModelUpdatesDoNotLoseOtherModels()
    {
        const int workers = 40;
        var tasks = Enumerable.Range(1, workers).Select(i => Task.Run(() =>
        {
            var key = i.ToString("X64", System.Globalization.CultureInfo.InvariantCulture);
            var options = new ModelRuntimeOptions(2048 + i * 16, 4, 256, 0.25);
            ModelSettingsStore.Save(key, options);
            Assert.Equal(options, ModelSettingsStore.Load(key));
        }));
        await Task.WhenAll(tasks);
        foreach (var i in Enumerable.Range(1, workers))
        {
            var key = i.ToString("X64", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(2048 + i * 16, ModelSettingsStore.Load(key).ContextSize);
        }
    }

    [Fact]
    public void InvalidOptionsNeverOverwriteValidModelSettings()
    {
        const string key = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";
        var initial = new ModelRuntimeOptions(2048, 3, 512, 0.5);
        ModelSettingsStore.Save(key, initial);
        Assert.Throws<ArgumentOutOfRangeException>(() => ModelSettingsStore.Save(key,
            new ModelRuntimeOptions(100, 3, 512, 0.5)));
        Assert.Throws<ArgumentException>(() => ModelSettingsStore.Load("../unsafe"));
        Assert.Equal(initial, ModelSettingsStore.Load(key));
    }
}
