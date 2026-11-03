using WorkIntelligence.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class WorkRecordTests : IDisposable
{
    private readonly KnowledgeStore _store;
    public WorkRecordTests()
    {
        if (Directory.Exists(LocalPaths.Root)) Directory.Delete(LocalPaths.Root,true);
        _store = new KnowledgeStore();
    }

    [Theory]
    [InlineData("Project")]
    [InlineData("Milestone")]
    [InlineData("Action")]
    [InlineData("Risk")]
    [InlineData("Decision")]
    [InlineData("Incident")]
    public void WorkKindsPersistOwnerDueDateAndRevisionHistory(string kind)
    {
        var original = _store.SaveWorkRecord(1,kind,"Synthetic record","Original details","Test owner",
            "2026-12-01","Open",null,"Initial synthetic record");
        var updated = _store.SaveWorkRecord(1,kind,"Synthetic record","Resolution and rationale","New owner",
            null,"Completed",null,"Verified resolution",original.Id,original.Revision);
        using var reopened = new KnowledgeStore();
        Assert.Equal(updated,Assert.Single(reopened.WorkRecords(1)));
        var history = reopened.WorkHistory(original.Id,1);
        Assert.Equal(2,history.Count);
        Assert.Equal(original,history[0].Record);
        Assert.Equal(updated,history[1].Record);
        Assert.Equal("Verified resolution",history[1].Reason);
    }

    [Fact]
    public void StaleEditsCannotOverwriteNewerDecisionsOrHistory()
    {
        var original = _store.SaveWorkRecord(1,"Decision","Synthetic decision","Original","Owner",null,"Open",null,"Initial");
        _store.SaveWorkRecord(1,"Decision",original.Title,"Approved replacement","Owner",null,"Completed",null,"Reviewed change",original.Id,1);
        Assert.Throws<InvalidDataException>(() => _store.SaveWorkRecord(1,"Decision",original.Title,"Stale edit","Owner",null,"Open",null,"Stale reason",original.Id,1));
        Assert.Equal(2,_store.WorkHistory(original.Id,1).Count);
        Assert.Equal("Approved replacement",Assert.Single(_store.WorkRecords(1)).Details);
    }

    [Fact]
    public void RelatedCasesAndHistoryCannotCrossWorkspaceScopes()
    {
        var other = _store.AddWorkspace("Isolated record workspace");
        var incident = _store.SaveWorkRecord(1,"Incident","Synthetic incident","Prior case","Owner",null,"Completed",null,"Initial");
        var action = _store.SaveWorkRecord(1,"Action","Followup","Resolution steps","Owner",null,"Open",incident.Id,"Based on prior case");
        Assert.Equal(incident.Id,action.RelatedId);
        Assert.Throws<InvalidDataException>(() => _store.SaveWorkRecord(other,"Action","Wrong scope","Details","Owner",null,"Open",incident.Id,"Initial"));
        Assert.Empty(_store.WorkRecords(other));
        Assert.Empty(_store.WorkHistory(incident.Id,other));
        Assert.Throws<InvalidDataException>(() => _store.SaveWorkRecord(other,"Incident","Wrong scope","Changed","Owner",null,"Open",null,"Change",incident.Id,incident.Revision));
    }

    [Fact]
    public async Task BackupRestoresWorkRecordsAndTheirFullHistory()
    {
        var repository = Path.Combine(Path.GetTempPath(),"wi-work-record-backup-"+Guid.NewGuid().ToString("N"));
        try
        {
            var record = _store.SaveWorkRecord(1,"Decision","Synthetic decision","Original","Owner",null,"Open",null,"Initial");
            _store.SaveWorkRecord(1,"Decision",record.Title,"Superseded procedure","Owner",null,"Superseded",null,"New procedure approved",record.Id,1);
            await new BackupService(_store).BackupAsync(repository,CancellationToken.None);
            Directory.Delete(LocalPaths.Root,true);
            await BackupService.RestoreToEmptyVaultAsync(repository,CancellationToken.None);
            using var restored = new KnowledgeStore();
            Assert.Equal("Superseded",Assert.Single(restored.WorkRecords(1)).Status);
            Assert.Equal(2,restored.WorkHistory(record.Id,1).Count);
        }
        finally { Directory.Delete(repository,true); }
    }

    [Fact]
    public void SchemaSixMigrationPreservesExistingTasksAndKnowledge()
    {
        _store.AddMemory("Synthetic retained memory","fixture");
        _store.AddTask(1,"Legacy task","Legacy details");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=LocalPaths.Database,Pooling=false}.ToString()))
        {
            connection.Open();
            using var cmd=connection.CreateCommand();
            cmd.CommandText="DROP TABLE IF EXISTS memory_policies; DROP TRIGGER work_records_ai; DROP TRIGGER work_records_ad; DROP TRIGGER work_records_au; DROP TABLE work_records_fts; DROP TABLE work_record_revisions; DROP TABLE work_records; PRAGMA user_version=6";
            cmd.ExecuteNonQuery();
        }
        using var upgraded = new KnowledgeStore();
        Assert.Equal("Legacy task",Assert.Single(upgraded.Tasks(1)).Title);
        Assert.Equal("Synthetic retained memory",Assert.Single(upgraded.Memories()).Text);
        Assert.Empty(upgraded.WorkRecords(1));
        Assert.Equal("ok",upgraded.CheckDatabase());
    }

    [Theory]
    [InlineData("2026-02-30", "Initial")]
    [InlineData("not-a-date", "Initial")]
    [InlineData(null, "")]
    public void InvalidRecordDoesNotCreateHistory(string? due,string reason)
    {
        Assert.Throws<ArgumentException>(() => _store.SaveWorkRecord(1,"Project","Synthetic","Details","Owner",due,"Open",null,reason));
        Assert.Empty(_store.WorkRecords(1));
    }

    [Fact]
    public void RetrievalUsesCurrentWorkspaceRevisionsAndExcludesSupersededRecords()
    {
        var other = _store.AddWorkspace("Private project records");
        var record = _store.SaveWorkRecord(1,"Incident","Synthetic network incident","RouterAlpha resolution","Owner",null,"Completed",null,"Initial");
        Assert.Single(_store.SearchWorkRecords("RouterAlpha",1));
        Assert.Empty(_store.SearchWorkRecords("RouterAlpha",other));
        _store.SaveWorkRecord(1,"Incident",record.Title,"RouterBeta resolution","Owner",null,"Completed",null,"Corrected",record.Id,1);
        Assert.Empty(_store.SearchWorkRecords("RouterAlpha",1));
        Assert.Single(_store.SearchWorkRecords("RouterBeta",1));
        _store.SaveWorkRecord(1,"Incident",record.Title,"RouterBeta resolution","Owner",null,"Superseded",null,"Retired procedure",record.Id,2);
        Assert.Empty(_store.SearchWorkRecords("RouterBeta",1));
        Assert.Equal(3,_store.WorkHistory(record.Id,1).Count);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(LocalPaths.Root)) Directory.Delete(LocalPaths.Root,true);
    }
}
