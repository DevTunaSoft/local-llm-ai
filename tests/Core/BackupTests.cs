using System.Text;
using System.Text.Json.Nodes;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class BackupTests : IDisposable
{
    private readonly string _repository = Path.Combine(Path.GetTempPath(), "wi-repository-" + Guid.NewGuid().ToString("N"));

    public BackupTests()
    {
        Assert.StartsWith(Path.GetTempPath(), LocalPaths.Root, StringComparison.Ordinal);
        if (Directory.Exists(LocalPaths.Root)) Directory.Delete(LocalPaths.Root, true);
    }

    private async Task<string> SeedBackup(string text = "Synthetic recoverable knowledge zebra")
    {
        LocalPaths.Ensure();
        using var store = new KnowledgeStore();
        var file = Path.Combine(LocalPaths.Root, "synthetic.txt");
        File.WriteAllText(file, text);
        store.Import(file);
        store.AddMemory("Synthetic approved recovery memory", "fixture");
        var chat = store.CreateChat("Synthetic recovery conversation");
        store.AddMessage(chat, "user", "Synthetic question");
        await new BackupService(store).BackupAsync(_repository, CancellationToken.None);
        return Directory.GetDirectories(_repository, "snap-*").Single();
    }

    [Fact]
    public async Task PlaintextBackupRestoresDocumentsMemoriesAndChat()
    {
        var snapshot = await SeedBackup();
        Assert.True(File.Exists(Path.Combine(snapshot, "manifest.json")));
        var databaseHeader = new byte[16];
        using (var database = File.OpenRead(Path.Combine(snapshot, "knowledge.db")))
            Assert.Equal(databaseHeader.Length, database.Read(databaseHeader, 0, databaseHeader.Length));
        Assert.Equal(new byte[] { 83, 81, 76, 105, 116, 101, 32, 102, 111, 114, 109, 97, 116, 32, 51, 0 }, databaseHeader);
        Directory.Delete(LocalPaths.Root, true);
        await BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None);
        using var store = new KnowledgeStore();
        Assert.Single(store.Search("zebra"));
        Assert.Equal("Synthetic approved recovery memory", Assert.Single(store.Memories()).Text);
        Assert.Equal("Synthetic question", Assert.Single(store.Messages(Assert.Single(store.Chats()).Id)).Content);
        var document = Assert.Single(store.Documents());
        Assert.Equal("Synthetic recoverable knowledge zebra", Encoding.UTF8.GetString(
            File.ReadAllBytes(Path.Combine(LocalPaths.Blobs, $"{document.Id}.bin"))));
    }

    [Fact]
    public async Task TamperedSnapshotDoesNotPromoteFiles()
    {
        var snapshot = await SeedBackup();
        File.AppendAllText(Path.Combine(snapshot, "knowledge.db"), "tampered");
        Directory.Delete(LocalPaths.Root, true);
        await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None));
        Assert.False(File.Exists(LocalPaths.Database));
        Assert.Empty(Directory.EnumerateFiles(LocalPaths.Blobs));
    }

    [Fact]
    public async Task NewestSnapshotWinsAndEarlierVersionsArePreserved()
    {
        var first = await SeedBackup();
        using (var store = new KnowledgeStore())
        {
            store.AddMemory("Synthetic later memory", "fixture");
            await new BackupService(store).BackupAsync(_repository, CancellationToken.None);
        }
        Assert.Equal(2, Directory.GetDirectories(_repository, "snap-*").Length);
        Directory.Delete(LocalPaths.Root, true);
        await BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None);
        using var restored = new KnowledgeStore();
        Assert.Equal(2, restored.Memories().Count);
        Assert.True(Directory.Exists(first));
    }

    [Fact]
    public async Task RestoreUsesOlderSnapshotWhenNewestManifestIsCorrupted()
    {
        var first = await SeedBackup("Synthetic recovery from older snapshot");
        using (var store = new KnowledgeStore())
        {
            store.AddMemory("Synthetic newer-only memory", "fixture");
            await new BackupService(store).BackupAsync(_repository, CancellationToken.None);
        }
        var newest = Directory.GetDirectories(_repository, "snap-*").Single(x => x != first);
        File.AppendAllText(Path.Combine(newest, "manifest.json"), "corrupted");
        Directory.Delete(LocalPaths.Root, true);
        await BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None);
        using var restored = new KnowledgeStore();
        Assert.Single(restored.Search("recovery older"));
        Assert.Single(restored.Memories());
        Assert.DoesNotContain(restored.Memories(), x => x.Text == "Synthetic newer-only memory");
        Assert.True(Directory.Exists(first));
        Assert.True(Directory.Exists(newest));
    }

    [Fact]
    public async Task PortableSnapshotRestoresPerModelRuntimeSettings()
    {
        var hash = new string('D', 64);
        await SeedBackup();
        using (var store = new KnowledgeStore())
        {
            ModelSettingsStore.Save(hash, new ModelRuntimeOptions(8192, 6, 768, 0.2));
            await new BackupService(store).BackupAsync(_repository, CancellationToken.None);
        }
        Directory.Delete(LocalPaths.Root, true);
        await BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None);
        Assert.Equal(new ModelRuntimeOptions(8192, 6, 768, 0.2), ModelSettingsStore.Load(hash));
    }

    [Fact]
    public async Task OptInRetentionKeepsNewestVerifiedSnapshotsAndNeverDeletesCorruptOnes()
    {
        await SeedBackup();
        using (var store = new KnowledgeStore())
        {
            for (var i = 0; i < 3; i++)
            {
                store.AddMemory("Synthetic retention item " + i, "fixture");
                await new BackupService(store).BackupAsync(_repository, CancellationToken.None);
            }
        }
        var snapshots = Directory.GetDirectories(_repository, "snap-*")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToArray();
        Assert.Equal(4, snapshots.Length);
        var corrupt = snapshots[3];
        File.AppendAllText(Path.Combine(corrupt, "manifest.json"), "invalid");
        Assert.Throws<ArgumentOutOfRangeException>(() => BackupRetention.Prune(_repository, 1, CancellationToken.None));
        Assert.Equal(1, BackupRetention.Prune(_repository, 2, CancellationToken.None));
        var remaining = Directory.GetDirectories(_repository, "snap-*");
        Assert.Equal(3, remaining.Length);
        Assert.Contains(corrupt, remaining);
        Assert.Contains(snapshots[0], remaining);
        Assert.Contains(snapshots[1], remaining);
    }

    [Theory]
    [InlineData("null-path")]
    [InlineData("null-hash")]
    [InlineData("duplicate-file")]
    [InlineData("traversal")]
    [InlineData("incorrect-length")]
    public async Task MalformedLatestSnapshotFallsBackWithoutWritingCorruptData(string attack)
    {
        var original = await SeedBackup("Synthetic intact fallback fixture");
        using (var store = new KnowledgeStore())
        {
            store.AddMemory("Synthetic newer state should not restore", "fixture");
            await new BackupService(store).BackupAsync(_repository, CancellationToken.None);
        }
        var newest = Directory.GetDirectories(_repository, "snap-*").Single(p => p != original);
        var manifestPath = Path.Combine(newest, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var files = manifest["Files"]!.AsArray();
        var entry = files.Single(x => x!["Path"]!.GetValue<string>() == "knowledge.db")!.AsObject();
        switch (attack)
        {
            case "null-path": entry["Path"] = null; break;
            case "null-hash": entry["Sha256"] = null; break;
            case "duplicate-file": files.Add(entry.DeepClone()); break;
            case "traversal": entry["Path"] = "../knowledge.db"; break;
            case "incorrect-length": entry["Length"] = -1; break;
        }
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        Directory.Delete(LocalPaths.Root, true);
        await BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None);
        using var restored = new KnowledgeStore();
        Assert.Single(restored.Search("intact fallback fixture"));
        Assert.DoesNotContain(restored.Memories(), m => m.Text.Contains("newer state"));
    }

    [Fact]
    public async Task PreCancelledBackupDoesNotPublishOrOverwriteRecoveryPoints()
    {
        await SeedBackup();
        using var store = new KnowledgeStore();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var before = Directory.GetDirectories(_repository, "snap-*");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new BackupService(store).BackupAsync(_repository, cancelled.Token));
        var after = Directory.GetDirectories(_repository, "snap-*");
        Assert.Equal(before.OrderBy(x => x), after.OrderBy(x => x));
        Assert.Empty(Directory.GetDirectories(_repository, ".pending-*"));
    }

    [Fact]
    public async Task SimultaneousRestoresCannotOverwriteOrInterleaveVaultFiles()
    {
        await SeedBackup("Synthetic concurrency restore fixture");
        Directory.Delete(LocalPaths.Root, true);
        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            try
            {
                await BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None);
                return "restored";
            }
            catch (InvalidOperationException)
            {
                return "already-restored";
            }
        });
        var outcomes = await Task.WhenAll(attempts);
        Assert.Equal(1, outcomes.Count(x => x == "restored"));
        Assert.Equal(7, outcomes.Count(x => x == "already-restored"));
        using var restored = new KnowledgeStore();
        Assert.Equal("ok", restored.CheckDatabase());
        Assert.Single(restored.Search("concurrency restore fixture"));
        Assert.Single(restored.Memories());
    }

    [Fact]
    public async Task RestoreRefusesToOverwriteLiveData()
    {
        await SeedBackup();
        await Assert.ThrowsAsync<InvalidOperationException>(() => BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None));
        Assert.True(File.Exists(LocalPaths.Database));
    }

    [Fact]
    public async Task UnknownLegacyResticRepositoryIsNotSilentlyRestored()
    {
        Directory.CreateDirectory(_repository);
        File.WriteAllText(Path.Combine(_repository, "config"), "legacy synthetic repository");
        LocalPaths.Ensure();
        await Assert.ThrowsAsync<InvalidDataException>(() => BackupService.RestoreToEmptyVaultAsync(_repository, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_repository)) Directory.Delete(_repository, true);
        if (Directory.Exists(LocalPaths.Root)) Directory.Delete(LocalPaths.Root, true);
    }
}
