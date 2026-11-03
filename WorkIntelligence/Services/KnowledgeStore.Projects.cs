using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WorkIntelligence.Models;

namespace WorkIntelligence.Services;

public sealed partial class KnowledgeStore
{
    public static readonly IReadOnlyList<string> WorkKinds = ["Project", "Milestone", "Action", "Risk", "Decision", "Incident"];
    public static readonly IReadOnlyList<string> WorkStatuses = ["Open", "In Progress", "Blocked", "Completed", "Superseded"];

    public List<WorkRecord> WorkRecords(long workspaceId)
    {
        using var con = Connect();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id,workspace_id,kind,title,details,owner,due_date,status,related_id,revision,updated_at FROM work_records WHERE workspace_id=$w ORDER BY id DESC";
        cmd.Parameters.AddWithValue("$w", workspaceId);
        using var reader = cmd.ExecuteReader();
        var records = new List<WorkRecord>();
        while (reader.Read()) records.Add(ReadWorkRecord(reader));
        return records;
    }

    public List<WorkRecord> SearchWorkRecords(string query,long workspaceId,int limit = 6)
    {
        if (limit is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(limit));
        var terms = System.Text.RegularExpressions.Regex.Matches(query[..Math.Min(query.Length,500)], @"[\p{L}\p{N}_]{2,}")
            .Select(x => "\"" + x.Value + "\"").Distinct().Take(16).ToArray();
        if (terms.Length == 0) return [];
        using var con = Connect();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT r.id,r.workspace_id,r.kind,r.title,r.details,r.owner,r.due_date,r.status,r.related_id,r.revision,r.updated_at
            FROM work_records_fts JOIN work_records r ON r.id=work_records_fts.rowid
            WHERE work_records_fts MATCH $q AND r.workspace_id=$w AND r.status<>'Superseded'
            ORDER BY bm25(work_records_fts),r.id DESC LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$q",string.Join(" OR ",terms));
        cmd.Parameters.AddWithValue("$w",workspaceId); cmd.Parameters.AddWithValue("$limit",limit);
        using var reader = cmd.ExecuteReader();
        var results = new List<WorkRecord>();
        while (reader.Read()) results.Add(ReadWorkRecord(reader));
        return results;
    }

    private static WorkRecord ReadWorkRecord(SqliteDataReader r) => new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),
        r.GetString(3),r.GetString(4),r.GetString(5),r.IsDBNull(6)?null:r.GetString(6),r.GetString(7),
        r.IsDBNull(8)?null:r.GetInt64(8),r.GetInt32(9),r.GetString(10));

    /// <summary>Creates or revises a workspace record with an immutable, atomic revision history.
    /// expectedRevision prevents one editor from silently replacing another editor's newer work.</summary>
    public WorkRecord SaveWorkRecord(long workspaceId,string kind,string title,string details,string owner,
        string? dueDate,string status,long? relatedId,string reason,long? id = null,int? expectedRevision = null)
    {
        title = title.Trim(); owner = owner.Trim(); reason = reason.Trim();
        if (!WorkKinds.Contains(kind) || !WorkStatuses.Contains(status) || title.Length is < 1 or > 200 ||
            details.Length > 20000 || owner.Length > 200 || reason.Length is < 1 or > 2000)
            throw new ArgumentException("Record kind/status, title, owner, details or change reason is invalid.");
        if (dueDate is not null && !DateOnly.TryParseExact(dueDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
            throw new ArgumentException("Due date must be yyyy-MM-dd or empty.");
        if ((id is null) != (expectedRevision is null) || expectedRevision is < 1)
            throw new ArgumentException("Editing requires the current record ID and revision.");
        if (id is not null && relatedId == id) throw new ArgumentException("A record cannot link to itself.");
        lock (_writeGate)
        {
            using var con = Connect();
            using var tx = con.BeginTransaction();
            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT id FROM workspaces WHERE id=$w";
            cmd.Parameters.AddWithValue("$w",workspaceId);
            if (cmd.ExecuteScalar() is null) throw new InvalidDataException("Workspace not found.");
            if (relatedId is long related)
            {
                cmd.CommandText = "SELECT id FROM work_records WHERE id=$related AND workspace_id=$w";
                cmd.Parameters.AddWithValue("$related",related);
                if (cmd.ExecuteScalar() is null) throw new InvalidDataException("Linked record must belong to the same workspace.");
            }
            var at = DateTimeOffset.UtcNow.ToString("O");
            var revision = checked((expectedRevision ?? 0) + 1);
            cmd.Parameters.AddWithValue("$kind",kind); cmd.Parameters.AddWithValue("$title",title);
            cmd.Parameters.AddWithValue("$details",details); cmd.Parameters.AddWithValue("$owner",owner);
            cmd.Parameters.AddWithValue("$due",(object?)dueDate ?? DBNull.Value); cmd.Parameters.AddWithValue("$status",status);
            cmd.Parameters.AddWithValue("$link",(object?)relatedId ?? DBNull.Value); cmd.Parameters.AddWithValue("$revision",revision);
            cmd.Parameters.AddWithValue("$at",at);
            long recordId;
            if (id is null)
            {
                cmd.CommandText = """
                    INSERT INTO work_records(workspace_id,kind,title,details,owner,due_date,status,related_id,revision,updated_at)
                    VALUES($w,$kind,$title,$details,$owner,$due,$status,$link,$revision,$at); SELECT last_insert_rowid();
                    """;
                recordId = (long)(cmd.ExecuteScalar() ?? throw new InvalidOperationException("Record creation failed."));
            }
            else
            {
                cmd.Parameters.AddWithValue("$id",id.Value);
                cmd.Parameters.AddWithValue("$expected",expectedRevision!.Value);
                cmd.CommandText = """
                    UPDATE work_records SET kind=$kind,title=$title,details=$details,owner=$owner,due_date=$due,
                      status=$status,related_id=$link,revision=$revision,updated_at=$at
                    WHERE id=$id AND workspace_id=$w AND revision=$expected
                    """;
                if (cmd.ExecuteNonQuery() != 1)
                    throw new InvalidDataException("Record changed or is not in this workspace. Reload before editing.");
                recordId = id.Value;
            }
            var record = new WorkRecord(recordId,workspaceId,kind,title,details,owner,dueDate,status,relatedId,revision,at);
            using var history = con.CreateCommand();
            history.Transaction = tx;
            history.CommandText = "INSERT INTO work_record_revisions(record_id,revision,payload,reason,created_at) VALUES($id,$revision,$payload,$reason,$at)";
            history.Parameters.AddWithValue("$id",recordId); history.Parameters.AddWithValue("$revision",revision);
            history.Parameters.AddWithValue("$payload",JsonSerializer.Serialize(record));
            history.Parameters.AddWithValue("$reason",reason); history.Parameters.AddWithValue("$at",at);
            history.ExecuteNonQuery();
            tx.Commit();
            return record;
        }
    }

    public List<WorkRecordRevision> WorkHistory(long recordId,long workspaceId)
    {
        using var con = Connect();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT h.revision,h.reason,h.created_at,h.payload FROM work_record_revisions h
            JOIN work_records r ON r.id=h.record_id WHERE r.id=$id AND r.workspace_id=$w ORDER BY h.revision
            """;
        cmd.Parameters.AddWithValue("$id",recordId); cmd.Parameters.AddWithValue("$w",workspaceId);
        using var reader = cmd.ExecuteReader();
        var history = new List<WorkRecordRevision>();
        while (reader.Read()) history.Add(new(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),
            JsonSerializer.Deserialize<WorkRecord>(reader.GetString(3)) ?? throw new InvalidDataException("Record history is malformed.")));
        return history;
    }
}
