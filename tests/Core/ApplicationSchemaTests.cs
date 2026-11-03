using Microsoft.Data.Sqlite;
using WorkIntelligence.Services;
using Xunit;

namespace WorkIntelligence.Tests;

public sealed class ApplicationSchemaTests
{
    [Fact]
    public void UnrelatedSqliteDataIsRefusedWithoutCreatingApplicationTables()
    {
        using var db=new SqliteConnection("Data Source=:memory:");db.Open();
        using var command=db.CreateCommand();command.CommandText="CREATE TABLE unrelated(content TEXT); INSERT INTO unrelated VALUES('synthetic preserved data')";command.ExecuteNonQuery();
        Assert.Throws<InvalidDataException>(()=>KnowledgeStore.InitializeSchema(db));
        command.CommandText="SELECT content FROM unrelated";Assert.Equal("synthetic preserved data",command.ExecuteScalar());
        command.CommandText="SELECT count(*) FROM sqlite_master WHERE name='memories'";Assert.Equal(0L,command.ExecuteScalar());
    }
    [Fact]
    public void MissingApplicationTablesAreNotAcceptedAsAnIntactSchema()
    {
        using var db=new SqliteConnection("Data Source=:memory:");db.Open();KnowledgeStore.InitializeSchema(db);
        using var command=db.CreateCommand();command.CommandText="DROP TABLE memory_policies";command.ExecuteNonQuery();
        Assert.Throws<InvalidDataException>(()=>ApplicationSchema.Verify(db));
    }
}
