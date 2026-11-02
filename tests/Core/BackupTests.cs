using System.Text;
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
