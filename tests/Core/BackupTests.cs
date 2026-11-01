using System.Diagnostics;
using System.Text;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class BackupTests : IDisposable
{
    private const string VaultPassword = "Synthetic vault password for recovery";
    private const string BackupPassword = "Independent synthetic backup password";
    private readonly string _repository = Path.Combine(Path.GetTempPath(), "wi-repository-" + Guid.NewGuid().ToString("N"));
    private readonly string _restic = Environment.GetEnvironmentVariable("WI_TEST_RESTIC")
        ?? throw new InvalidOperationException("Set WI_TEST_RESTIC to the verified native restic test binary.");

    public BackupTests()
    {
        Assert.StartsWith(Path.GetTempPath(), LocalPaths.Root, StringComparison.Ordinal);
        if (Directory.Exists(LocalPaths.Root)) Directory.Delete(LocalPaths.Root, true);
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlcipher());
    }

    private string Resolve(string component)
    {
        Assert.Equal("restic.exe", component);
        return _restic;
    }

    private async Task SeedBackup()
    {
        using var vault = Vault.Open(VaultPassword);
        using var store = new KnowledgeStore(vault);
        var file = Path.Combine(LocalPaths.Root, "synthetic.txt");
        File.WriteAllText(file, "Synthetic recoverable knowledge zebra");
        store.Import(file);
        store.AddMemory("Synthetic approved recovery memory", "fixture");
        var chat = store.CreateChat("Synthetic recovery conversation");
        store.AddMessage(chat, "user", "Synthetic question");
        await new BackupService(store, Resolve).BackupAsync(_repository, BackupPassword, CancellationToken.None);
    }

    [Fact]
    public async Task RealResticBackupRestoresEncryptedKnowledgeMemoriesAndChat()
    {
        await SeedBackup();
        Directory.Delete(LocalPaths.Root, true);
        await BackupService.RestoreToEmptyVaultAsync(_repository, BackupPassword, CancellationToken.None, Resolve);
        using var vault = Vault.Open(VaultPassword);
        using var store = new KnowledgeStore(vault);
        Assert.Single(store.Search("zebra"));
        Assert.Equal("Synthetic approved recovery memory", Assert.Single(store.Memories()).Text);
        Assert.Equal("Synthetic question", Assert.Single(store.Messages(Assert.Single(store.Chats()).Id)).Content);
        var doc = Assert.Single(store.Documents());
        var recovered = vault.Decrypt(File.ReadAllBytes(Path.Combine(LocalPaths.Blobs, $"{doc.Id}.enc")));
        Assert.Equal("Synthetic recoverable knowledge zebra", Encoding.UTF8.GetString(recovered));
    }

    [Fact]
    public async Task RestoreIgnoresANewerUnrelatedSnapshot()
    {
        await SeedBackup();
        var unrelated = Path.Combine(LocalPaths.Root, "unrelated");
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(unrelated, "unrelated.txt"), "Synthetic unrelated backup");
        await Restic("backup", unrelated, "--tag", "unrelated-test", "--time", "2099-01-01 00:00:00");
        Directory.Delete(LocalPaths.Root, true);
        await BackupService.RestoreToEmptyVaultAsync(_repository, BackupPassword, CancellationToken.None, Resolve);
        using var vault = Vault.Open(VaultPassword);
        using var store = new KnowledgeStore(vault);
        Assert.Single(store.Search("zebra"));
    }

    [Fact]
    public async Task WrongBackupPasswordNeverPromotesPartialVaultFiles()
    {
        await SeedBackup();
        Directory.Delete(LocalPaths.Root, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => BackupService.RestoreToEmptyVaultAsync(
            _repository, "Wrong synthetic backup password", CancellationToken.None, Resolve));
        Assert.False(File.Exists(LocalPaths.Database));
        Assert.False(Vault.Exists);
        Assert.Empty(Directory.GetFiles(LocalPaths.Blobs));
    }

    private async Task Restic(params string[] args)
    {
        var info = new ProcessStartInfo(_restic) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment["RESTIC_PASSWORD"] = BackupPassword;
        info.ArgumentList.Add("--repo"); info.ArgumentList.Add(_repository);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await stdout;
        var error = await stderr;
        Assert.True(process.ExitCode == 0, "Synthetic restic command failed: " + error);
    }

    public void Dispose()
    {
        if (Directory.Exists(LocalPaths.Root)) Directory.Delete(LocalPaths.Root, true);
        if (Directory.Exists(_repository)) Directory.Delete(_repository, true);
    }
}
