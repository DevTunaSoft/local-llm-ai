using Microsoft.Data.Sqlite;

namespace WorkIntelligence.Services;

internal static class ApplicationSchema
{
    internal const int Current=8;
    internal static void Verify(SqliteConnection connection,bool allowEmpty=false)
    {
        using var command=connection.CreateCommand();command.CommandText="PRAGMA user_version";
        var version=Convert.ToInt32(command.ExecuteScalar());
        if(version>Current)throw new InvalidDataException("Database schema is newer than this application; refusing to modify it.");
        command.CommandText="SELECT name FROM sqlite_master WHERE type='table'";
        var tables=new HashSet<string>(StringComparer.Ordinal);
        using(var reader=command.ExecuteReader())while(reader.Read())tables.Add(reader.GetString(0));
        if(allowEmpty && tables.Count==0 && version==0)return;
        var core=new[]{"documents","chunks","chunks_fts","memories","chats","messages","models","chunk_vectors","workspaces","workspace_documents","work_tasks","watched_folders","memory_proposals"};
        var required=core.AsEnumerable();
        if(version>=4)required=required.Append("memories_fts");
        if(version>=6)required=required.Append("messages_fts");
        if(version>=7)required=required.Concat(["work_records","work_record_revisions","work_records_fts"]);
        if(version>=8)required=required.Append("memory_policies");
        if(required.Any(x=>!tables.Contains(x)))throw new InvalidDataException("Database is not a complete recognized Work Intelligence schema. Existing data was not modified.");
    }
}
