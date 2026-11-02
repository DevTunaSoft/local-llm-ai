using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using WorkIntelligence.Services;
using Xunit;

// LocalPaths is process-wide; serialize storage fixtures and isolate the entire test process.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace WorkIntelligence.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly KnowledgeStore _store;

    public StorageTests()
    {
        Assert.StartsWith(Path.GetTempPath(), LocalPaths.Root, StringComparison.Ordinal);
        if (Directory.Exists(LocalPaths.Root)) Directory.Delete(LocalPaths.Root, true);
        LocalPaths.Ensure();
        _store = new KnowledgeStore();
    }

    private static string Source(string name, string text)
    {
        var path = Path.Combine(LocalPaths.Root, name);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    [Fact]
    public async Task ReplacedApprovedFolderDoesNotFollowASymlink()
    {
        var parent = Path.Combine(Path.GetTempPath(), "wi-reparse-" + Guid.NewGuid().ToString("N"));
        var approved = Path.Combine(parent, "approved");
        var outside = Path.Combine(parent, "outside");
        Directory.CreateDirectory(approved);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "private.txt"), "Synthetic data outside approved folder");
        try
        {
            _store.AddWatchFolder(approved, 1);
            Directory.Delete(approved);
            Directory.CreateSymbolicLink(approved, outside);
            var scan = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.Equal(0, scan.Imported);
            Assert.True(scan.Failed > 0);
            Assert.Empty(_store.Documents());
        }
        finally
        {
            if (Directory.Exists(approved)) Directory.Delete(approved);
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void LiveVaultIsRejectedWithinAnyConfiguredOneDriveFolder()
    {
        var parent = Path.Combine(Path.GetTempPath(), "wi-live-sync-" + Guid.NewGuid().ToString("N"));
        var sync = Path.Combine(parent, "OneDrive");
        var local = Path.Combine(parent, "OneDrive-LocalCopy");
        // A sibling whose name merely shares a prefix must remain allowed.
        LocalPaths.ValidateDataRoot(local, new string?[] { sync });
        LocalPaths.ValidateDataRoot(parent, new string?[] { sync });
        Assert.Throws<InvalidOperationException>(() =>
            LocalPaths.ValidateDataRoot(sync, new string?[] { sync }));
        Assert.Throws<InvalidOperationException>(() =>
            LocalPaths.ValidateDataRoot(Path.Combine(sync, "WorkIntelligence"), new string?[] { sync }));
        Assert.Throws<InvalidOperationException>(() =>
            LocalPaths.ValidateDataRoot(Path.Combine(sync, "nested", "Vault"), new string?[] { "", sync }));
        LocalPaths.ValidateDataRoot(Path.Combine(parent, "Unrelated"), new string?[] { null, "" });
        var volumeRoot = Path.GetPathRoot(parent)!;
        Assert.Throws<InvalidOperationException>(() =>
            LocalPaths.ValidateDataRoot(Path.Combine(parent, "Child"), new string?[] { volumeRoot }));
    }

    [Fact]
    public void LiveVaultRejectsReparsePointAncestors()
    {
        var parent = Path.Combine(Path.GetTempPath(), "wi-live-link-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(parent, "target");
        var alias = Path.Combine(parent, "alias");
        Directory.CreateDirectory(target);
        try
        {
            try { Directory.CreateSymbolicLink(alias, target); }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                // Windows environments without symbolic-link privileges cannot execute this case.
                return;
            }
            Assert.Throws<InvalidDataException>(() =>
                LocalPaths.ValidateDataRoot(Path.Combine(alias, "Vault"), Array.Empty<string?>()));
        }
        finally { Directory.Delete(parent, recursive: true); }
    }

    [Fact]
    public void NewerSchemaIsRefusedWithoutDowngradingIt()
    {
        using (var con = Connect())
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = "PRAGMA user_version=999";
            cmd.ExecuteNonQuery();
        }
        Assert.Throws<InvalidDataException>(() => new KnowledgeStore());
        using var connection = Connect();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        Assert.Equal(999L, command.ExecuteScalar());
    }

    [Fact]
    public void ApprovedLearningPersistsAcrossStoreReopenAndCorrectionsRemainScoped()
    {
        var workspace = _store.AddWorkspace("Persistent learning fixture");
        var original = _store.AddMemory("Synthetic dispatch rotation is Monday", "approved fixture", workspace);
        using (var reopened = new KnowledgeStore())
        {
            Assert.Contains(reopened.SearchMemories("dispatch rotation Monday", workspaceId: workspace), x => x.Id == original);
            var correction = reopened.ProposeMemory("Synthetic dispatch rotation is Tuesday", "approved correction", original, workspace);
            reopened.ResolveMemoryProposal(correction, true);
        }
        using (var reopenedAgain = new KnowledgeStore())
        {
            var active = reopenedAgain.SearchMemories("dispatch rotation", workspaceId: workspace);
            Assert.Single(active);
            Assert.Contains("Tuesday", active[0].Text);
            Assert.DoesNotContain(active, x => x.Id == original);
            Assert.Empty(reopenedAgain.SearchMemories("dispatch rotation", workspaceId: 1));
            Assert.Contains(reopenedAgain.MemoryHistory(workspaceId: workspace), x => x.Id == original && !x.Active);
        }
    }

    [Fact]
    public void MemoryProposalsRequireApprovalAndCannotBeApprovedTwice()
    {
        var id = _store.ProposeMemory("Synthetic pending procedure", "fixture");
        Assert.Empty(_store.Memories());
        _store.ResolveMemoryProposal(id, true);
        Assert.Single(_store.Memories());
        Assert.Empty(_store.MemoryProposals());
        Assert.Throws<InvalidDataException>(() => _store.ResolveMemoryProposal(id, true));
        Assert.Single(_store.Memories());
        var rejected = _store.ProposeMemory("Synthetic rejected memory", "fixture");
        _store.ResolveMemoryProposal(rejected, false);
        Assert.Single(_store.Memories());
    }

    [Fact]
    public void ApprovedCorrectionsSupersedeOldKnowledgeAndKeepHistory()
    {
        var operations = _store.AddWorkspace("Operations");
        var oldId = _store.AddMemory("Dispatch phone policy is 100", "synthetic policy", operations);
        var proposal = _store.ProposeMemory("Dispatch phone policy is 200", "user correction", oldId, operations);
        Assert.Single(_store.SearchMemories("Dispatch phone policy", workspaceId: operations));
        Assert.Equal(oldId, _store.SearchMemories("Dispatch phone policy", workspaceId: operations)[0].Id);
        _store.ResolveMemoryProposal(proposal, true);
        var active = _store.SearchMemories("Dispatch phone policy", workspaceId: operations);
        Assert.Single(active);
        Assert.Equal("Dispatch phone policy is 200", active[0].Text);
        var history = _store.MemoryHistory(workspaceId: operations);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, x => x.Id == oldId && !x.Active && x.SupersededBy == active[0].Id);
        Assert.Throws<InvalidDataException>(() => _store.ResolveMemoryProposal(proposal, true));
    }

    [Fact]
    public void MemoryScopesAndProposedCorrectionsCannotCrossWorkspaces()
    {
        var operations = _store.AddWorkspace("Operations");
        var leadership = _store.AddWorkspace("Leadership");
        Assert.Throws<InvalidDataException>(() => _store.CreateChat("invalid", 99999));
        Assert.Throws<InvalidDataException>(() => _store.AddMemory("orphan fact", "fixture", 99999));
        Assert.Throws<InvalidDataException>(() => _store.ProposeMemory("orphan proposal", "fixture", workspaceId: 99999));
        var secret = _store.AddMemory("Confidential staffing code Kestrel", "synthetic fixture", operations);
        Assert.Empty(_store.SearchMemories("staffing Kestrel", workspaceId: leadership));
        Assert.Single(_store.SearchMemories("staffing Kestrel", workspaceId: operations));
        Assert.Throws<InvalidDataException>(() =>
            _store.ProposeMemory("Confidential staffing code Updated", "fixture", secret, leadership));
        var pending = _store.ProposeMemory("Confidential staffing code Updated", "fixture", secret, operations);
        Assert.Empty(_store.MemoryProposals(leadership));
        Assert.Single(_store.MemoryProposals(operations));
        _store.ResolveMemoryProposal(pending, false);
        Assert.Equal(secret, _store.SearchMemories("staffing Kestrel", workspaceId: operations)[0].Id);
    }

    [Fact]
    public async Task DocumentAndConversationSearchStayInSelectedWorkspace()
    {
        var operations = _store.AddWorkspace("Operations");
        var leadership = _store.AddWorkspace("Leadership");
        var doc = _store.Import(Source("workspace-scope.txt", "Samsara configuration alpha synthetic"));
        _store.AssignDocument(doc.DocumentId, operations);
        Assert.Single(_store.Search("Samsara configuration alpha", workspaceId: operations));
        Assert.Empty(_store.Search("Samsara configuration alpha", workspaceId: leadership));
        Assert.Empty(await _store.SearchHybridAsync("Samsara configuration alpha", null, workspaceId: leadership));
        Assert.Empty(await _store.SearchHybridAsync("  ", null, workspaceId: operations));
        var chat = _store.CreateChat("Operations project", operations);
        _store.AddMessage(chat, "user", "Operations-only synthetic content", operations);
        Assert.Single(_store.Messages(chat, operations));
        Assert.Empty(_store.Messages(chat, leadership));
        Assert.Empty(_store.Chats(leadership));
        Assert.Throws<InvalidDataException>(() => _store.AddMessage(chat,"assistant","should not enter other workspace",leadership));
        Assert.Single(_store.Messages(chat,operations));
    }

    [Fact]
    public void RepeatedMemoryInstructionsAreIdempotentAndWorkspaceScoped()
    {
        var operations = _store.AddWorkspace("Memory operations");
        var leadership = _store.AddWorkspace("Memory leadership");
        Assert.True(_store.QueueMemoryIfNew("Synthetic on-call policy is Zeta", "chat 1", operations));
        Assert.False(_store.QueueMemoryIfNew("Synthetic on-call policy is Zeta", "chat 2", operations));
        Assert.Single(_store.MemoryProposals(operations));
        var proposal = _store.MemoryProposals(operations).Single();
        _store.ResolveMemoryProposal(proposal.Id, true);
        Assert.False(_store.QueueMemoryIfNew("Synthetic on-call policy is Zeta", "chat 3", operations));
        Assert.Empty(_store.MemoryProposals(operations));
        Assert.True(_store.QueueMemoryIfNew("Synthetic on-call policy is Zeta", "chat 4", leadership));
        Assert.Single(_store.MemoryProposals(leadership));
        Assert.Single(_store.Memories(operations));
        Assert.Empty(_store.Memories(leadership));
        Assert.Throws<InvalidDataException>(() => _store.QueueMemoryIfNew("Synthetic invalid", "chat", 99000));
    }

    [Fact]
    public void ConversationRecallOnlyUsesEarlierUserStatementsFromOneWorkspace()
    {
        var operations = _store.AddWorkspace("Recall operations");
        var leadership = _store.AddWorkspace("Recall leadership");
        var first = _store.CreateChat("Previous chat", operations);
        _store.AddMessage(first,"user","Dispatch handle Specter rule", operations);
        _store.AddMessage(first,"assistant","Dispatch handle Specter hallucination", operations);
        var current = _store.CreateChat("Current chat", operations);
        _store.AddMessage(current,"user","Dispatch handle Specter new context", operations);
        var privateChat = _store.CreateChat("Separate chat", leadership);
        _store.AddMessage(privateChat,"user","Dispatch handle Specter restricted", leadership);
        var hits = _store.RecallEarlierConversations("Dispatch Specter", operations, current);
        Assert.Single(hits);
        Assert.Equal(first, hits[0].ChatId);
        Assert.Equal("Dispatch handle Specter rule", hits[0].Text);
        Assert.Empty(_store.RecallEarlierConversations("Dispatch Specter", leadership, privateChat));
        Assert.Empty(_store.RecallEarlierConversations("the and or", operations, current));
        Assert.Throws<InvalidDataException>(() => _store.RecallEarlierConversations("Specter", 99000, current));
        _store.DeleteChat(first);
        Assert.Empty(_store.RecallEarlierConversations("Specter", operations, current));
    }

    [Fact]
    public async Task WatchedFolderDeletionRetiresContentAndReimportRestoresIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wi-retirement-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "procedure.txt");
            File.WriteAllText(path, "Synthetic RetireOmega first revision");
            var workspace = _store.AddWorkspace("Retirement workspace");
            _store.AddWatchFolder(folder, workspace);
            var initial = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.Equal(1, initial.Imported);
            Assert.Single(_store.Search("RetireOmega",workspaceId: workspace));
            File.Delete(path);
            var missing = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.Equal(1, missing.Retired);
            Assert.Empty(_store.Search("RetireOmega",workspaceId: workspace));
            Assert.Single(_store.Documents(), d => !d.Active);
            File.WriteAllText(path, "Synthetic RetireOmega new revision");
            var restored = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.Equal(1, restored.Imported);
            Assert.Equal(0, restored.Retired);
            Assert.Single(_store.Search("RetireOmega",workspaceId: workspace));
            Assert.Equal(2, _store.Documents().Count);
        }
        finally { Directory.Delete(folder,true); }
    }

    [Fact]
    public async Task RenamedWatchedDocumentNoLongerReturnsStaleCitation()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wi-rename-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var original = Path.Combine(folder,"old.txt");
            File.WriteAllText(original,"Synthetic RenameOmega document");
            _store.AddWatchFolder(folder,1);
            await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            File.Move(original,Path.Combine(folder,"new.txt"));
            var scan = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.Equal(1,scan.Imported);
            Assert.Equal(1,scan.Retired);
            Assert.Single(_store.Search("RenameOmega"));
            Assert.Equal("new.txt",_store.Search("RenameOmega")[0].Name);
        }
        finally { Directory.Delete(folder,true); }
    }

    [Fact]
    public async Task UnsafeSymlinkReplacementRetiresOnlyPreviouslyApprovedContent()
    {
        var parent = Path.Combine(Path.GetTempPath(), "wi-untrusted-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(parent,"approved");
        Directory.CreateDirectory(folder);
        var outside = Path.Combine(parent,"external.txt");
        var file = Path.Combine(folder,"procedure.txt");
        File.WriteAllText(file,"Synthetic SafeOmega approved content");
        File.WriteAllText(outside,"Synthetic OutsideOmega not authorized");
        try
        {
            _store.AddWatchFolder(folder,1);
            await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            File.Delete(file);
            File.CreateSymbolicLink(file,outside);
            var scan = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.Equal(1,scan.Retired);
            Assert.Empty(_store.Search("SafeOmega"));
            Assert.Empty(_store.Search("OutsideOmega"));
            Assert.Single(_store.Documents());
        }
        finally { Directory.Delete(parent,true); }
    }

    [Fact]
    public async Task EmptyReplacementRetiresOldIndexedContent()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wi-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder,"procedure.txt");
            File.WriteAllText(file,"Synthetic FullOmega approved content");
            _store.AddWatchFolder(folder,1);
            await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            File.WriteAllText(file,string.Empty);
            var scan = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.Equal(1,scan.Retired);
            Assert.Empty(_store.Search("FullOmega"));
        }
        finally { Directory.Delete(folder,true); }
    }

    [Fact]
    public async Task MissingApprovedFolderDoesNotCauseMassRetirement()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wi-offline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder,"fixture.txt"),"Synthetic OfflineOmega content");
            _store.AddWatchFolder(folder,1);
            await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Directory.Move(folder,folder + "-offline");
            var scan = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.True(scan.Failed > 0);
            Assert.Equal(0,scan.Retired);
            Assert.Single(_store.Search("OfflineOmega"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder,true);
            if (Directory.Exists(folder+"-offline")) Directory.Delete(folder+"-offline",true);
        }
    }

    [Fact]
    public void MemoryLearningNeedsAnExplicitUserAuthoredInstruction()
    {
        Assert.True(MemoryIntent.TryExtract("Remember that work documents stay local",out var proposed));
        Assert.Equal("work documents stay local",proposed);
        Assert.False(MemoryIntent.TryExtract("correction: replace an earlier policy",out _));
        Assert.False(MemoryIntent.TryExtract("Here is an article about memory",out _));
        Assert.False(MemoryIntent.TryExtract("remember: ",out _));
        Assert.True(MemoryIntent.TryExtract("We decided that revised steps are required", out var decision));
        Assert.Equal("revised steps are required", decision);
    }

    [Fact]
    public void PlaintextDatabaseHasStandardSqliteHeaderAndNoPassword()
    {
        _store.AddMemory("Synthetic test marker", "fixture");
        var bytes = File.ReadAllBytes(LocalPaths.Database);
        Assert.True(bytes.AsSpan(0, 16).SequenceEqual("SQLite format 3\0"u8));
        using var connection = Connect();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT content FROM memories LIMIT 1";
        Assert.Equal("Synthetic test marker", command.ExecuteScalar());
        Assert.Equal("ok", _store.CheckDatabase());
    }

    [Fact]
    public void LegacyVaultConfigurationIsRejectedWithoutDeletion()
    {
        File.WriteAllText(LocalPaths.VaultConfig, "synthetic legacy marker");
        Assert.Throws<InvalidOperationException>(() => new KnowledgeStore());
        Assert.Equal("synthetic legacy marker", File.ReadAllText(LocalPaths.VaultConfig));
        Assert.True(File.Exists(LocalPaths.Database));
    }

    [Fact]
    public void PlaintextDocumentAttachmentsAreNotEncrypted()
    {
        var imported = _store.Import(Source("raw.txt", "Synthetic plaintext attachment"));
        var file = Path.Combine(LocalPaths.Blobs, $"{imported.DocumentId}.bin");
        Assert.Equal("Synthetic plaintext attachment", Encoding.UTF8.GetString(File.ReadAllBytes(file)));
    }

    [Fact]
    public void FailedImportNeverDeletesAPreexistingAttachment()
    {
        var first = _store.Import(Source("one.txt", "Synthetic first document"));
        var nextBlob = Path.Combine(LocalPaths.Blobs, $"{first.DocumentId + 1}.bin");
        var original = "Preexisting recovery attachment"u8.ToArray();
        File.WriteAllBytes(nextBlob, original);
        Assert.Throws<IOException>(() => _store.Import(Source("two.txt", "Synthetic second document")));
        Assert.True(File.Exists(nextBlob), "Failed import deleted an attachment it did not create.");
        Assert.Equal(original, File.ReadAllBytes(nextBlob));
        Assert.Single(_store.Documents());
    }

    [Fact]
    public void ReimportAfterDeletingNewestVersionRestoresSearchability()
    {
        var path = Source("revision.txt", "Synthetic first revision alpha");
        _store.Import(path);
        File.WriteAllText(path, "Synthetic second revision beta");
        var second = _store.Import(path);
        _store.DeleteDocument(second.DocumentId);
        File.WriteAllText(path, "Synthetic first revision alpha");
        _store.Import(path);
        Assert.Single(_store.Search("alpha"));
        Assert.Single(_store.Documents(), d => d.Active);
    }

    [Fact]
    public void RevisedDocumentRetainsWorkspaceMembership()
    {
        var workspace = _store.AddWorkspace("Synthetic workspace");
        var path = Source("revision.txt", "Synthetic first revision");
        var first = _store.Import(path);
        _store.AssignDocument(first.DocumentId, workspace);
        File.WriteAllText(path, "Synthetic revised document");
        var revised = _store.Import(path);
        using var connection = Connect();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM workspace_documents WHERE workspace_id=$w AND document_id=$d";
        command.Parameters.AddWithValue("$w", workspace);
        command.Parameters.AddWithValue("$d", revised.DocumentId);
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact]
    public async Task ScanningAlreadyImportedFileAssignsItToApprovedWorkspace()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wi-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "fixture.txt");
            File.WriteAllText(file, "Synthetic monitored document");
            var doc = _store.Import(file);
            var workspace = _store.AddWorkspace("Monitor workspace");
            _store.AddWatchFolder(folder, workspace);
            var scan = await _store.ScanApprovedFoldersAsync(CancellationToken.None);
            Assert.Equal(0, scan.Failed);
            using var connection = Connect();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM workspace_documents WHERE document_id=$d AND workspace_id=$w";
            command.Parameters.AddWithValue("$d", doc.DocumentId);
            command.Parameters.AddWithValue("$w", workspace);
            Assert.Equal(1L, command.ExecuteScalar());
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void SnapshotContainsPlaintextDatabaseAndAttachments()
    {
        var document = _store.Import(Source("snapshot.txt", "Synthetic backup content"));
        var snapshot = Path.Combine(LocalPaths.Root, "snapshot");
        _store.CreateBackupSnapshot(snapshot, CancellationToken.None);
        var blob = Path.Combine(snapshot, "Blobs", $"{document.DocumentId}.bin");
        Assert.Equal("Synthetic backup content", Encoding.UTF8.GetString(File.ReadAllBytes(blob)));
        using var connection = Connect(Path.Combine(snapshot, "knowledge.db"));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT content FROM chunks";
        Assert.Equal("Synthetic backup content", command.ExecuteScalar());
    }

    [Fact]
    public void DeletingDocumentCascadesTextVectorsAndWorkspaceLinks()
    {
        var doc = _store.Import(Source("delete.txt", "Synthetic deletion zebra"));
        _store.AssignDocument(doc.DocumentId, 1);
        using (var con = Connect())
        using (var command = con.CreateCommand())
        {
            command.CommandText = "INSERT INTO chunk_vectors SELECT id,'fixture',16,zeroblob(64) FROM chunks WHERE document_id=$d";
            command.Parameters.AddWithValue("$d", doc.DocumentId);
            command.ExecuteNonQuery();
        }
        _store.DeleteDocument(doc.DocumentId);
        Assert.Empty(_store.Search("zebra"));
        Assert.False(File.Exists(Path.Combine(LocalPaths.Blobs, $"{doc.DocumentId}.bin")));
        using var connection = Connect();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT (SELECT count(*) FROM chunks)+(SELECT count(*) FROM chunk_vectors)+(SELECT count(*) FROM workspace_documents)";
        Assert.Equal(0L, cmd.ExecuteScalar());
    }

    private SqliteConnection Connect(string? path = null)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path ?? LocalPaths.Database, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        _store.Dispose();
        Directory.Delete(LocalPaths.Root, true);
    }
}
