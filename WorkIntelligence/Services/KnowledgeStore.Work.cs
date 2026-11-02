using Microsoft.Data.Sqlite;
using WorkIntelligence.Models;

namespace WorkIntelligence.Services;

public sealed partial class KnowledgeStore
{
    private static void RequireWorkspace(SqliteConnection connection, long workspaceId)
    {
        if (workspaceId <= 0) throw new ArgumentOutOfRangeException(nameof(workspaceId));
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT id FROM workspaces WHERE id=$w";
        check.Parameters.AddWithValue("$w",workspaceId);
        if (check.ExecuteScalar() is null) throw new InvalidDataException("Workspace does not exist.");
    }
    public List<WorkspaceRecord> Workspaces()
    {
        using var con=Connect(); using var cmd=con.CreateCommand();
        cmd.CommandText="SELECT id,name FROM workspaces ORDER BY name";
        using var r=cmd.ExecuteReader();var list=new List<WorkspaceRecord>();
        while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1)));
        return list;
    }
    public long AddWorkspace(string name)
    {
        name=name.Trim(); if(name.Length is < 1 or > 100)throw new ArgumentException("Workspace name must have 1-100 characters.");
        lock(_writeGate)
        {
            using var con=Connect();using var cmd=con.CreateCommand();
            cmd.CommandText="INSERT INTO workspaces(name) VALUES($n); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("$n",name);
            return (long)(cmd.ExecuteScalar() ?? throw new InvalidOperationException("Workspace creation failed."));
        }
    }
    public void AssignDocument(long documentId,long workspaceId)
    {
        lock(_writeGate)
        {
            using var con=Connect();using var cmd=con.CreateCommand();
            cmd.CommandText="INSERT OR IGNORE INTO workspace_documents(workspace_id,document_id) VALUES($w,$d)";
            cmd.Parameters.AddWithValue("$w",workspaceId);cmd.Parameters.AddWithValue("$d",documentId);
            cmd.ExecuteNonQuery();
        }
    }
    public string ReadAuthorizedDocumentText(long documentId, long workspaceId)
    {
        using var fileLock = FileOperationLock.Acquire("vault-files");
        using var con = Connect();
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT d.name,d.sha256 FROM documents d
            JOIN workspace_documents wd ON wd.document_id=d.id
            WHERE d.id=$id AND wd.workspace_id=$w
            """;
        cmd.Parameters.AddWithValue("$id", documentId);
        cmd.Parameters.AddWithValue("$w", workspaceId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("Selected document is not assigned to the current workspace.");
        var name = reader.GetString(0);
        var expected = reader.GetString(1);
        var path = Path.Combine(LocalPaths.Blobs, documentId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin");
        PathSafety.RejectReparsePoints(path);
        if (!File.Exists(path) || new FileInfo(path).Length > 25 * 1024 * 1024)
            throw new InvalidDataException("Stored source document is missing or oversized.");
        var bytes = File.ReadAllBytes(path);
        if (!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Stored document no longer matches its imported source hash.");
        return DocumentExtractor.Extract(name, bytes);
    }

    public List<WorkTaskRecord> Tasks(long workspaceId)
    {
        using var con=Connect();using var cmd=con.CreateCommand();
        cmd.CommandText="SELECT id,workspace_id,title,details,status,created_at FROM work_tasks WHERE workspace_id=$w ORDER BY id DESC";
        cmd.Parameters.AddWithValue("$w",workspaceId);
        using var r=cmd.ExecuteReader();var list=new List<WorkTaskRecord>();
        while(r.Read())list.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),r.GetString(4),DateTimeOffset.Parse(r.GetString(5))));
        return list;
    }
    public long AddTask(long workspaceId,string title,string details)
    {
        title=title.Trim();if(title.Length is < 1 or > 200||details.Length>20_000)throw new ArgumentException("Task title/details exceed limits.");
        lock(_writeGate)
        {
            using var con=Connect();using var cmd=con.CreateCommand();
            cmd.CommandText="INSERT INTO work_tasks(workspace_id,title,details,status,created_at) VALUES($w,$t,$d,'Open',$at); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("$w",workspaceId);cmd.Parameters.AddWithValue("$t",title);
            cmd.Parameters.AddWithValue("$d",details);cmd.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));
            return (long)(cmd.ExecuteScalar()??0L);
        }
    }
    public void UpdateTask(long taskId, long workspaceId, string title, string details)
    {
        title = title.Trim();
        if (title.Length is < 1 or > 200 || details.Length > 20_000)
            throw new ArgumentException("Task title/details exceed limits.");
        lock (_writeGate)
        {
            using var con = Connect();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "UPDATE work_tasks SET title=$t,details=$d WHERE id=$id AND workspace_id=$w";
            cmd.Parameters.AddWithValue("$t", title);
            cmd.Parameters.AddWithValue("$d", details);
            cmd.Parameters.AddWithValue("$id", taskId);
            cmd.Parameters.AddWithValue("$w", workspaceId);
            if (cmd.ExecuteNonQuery() != 1) throw new InvalidDataException("Task is not in the selected workspace.");
        }
    }

    public void SetTaskStatus(long taskId,string status)
    {
        if(status is not ("Open" or "In Progress" or "Blocked" or "Completed"))throw new ArgumentException("Invalid task status.");
        lock(_writeGate)
        {
            using var con=Connect();using var cmd=con.CreateCommand();
            cmd.CommandText="UPDATE work_tasks SET status=$s WHERE id=$id";
            cmd.Parameters.AddWithValue("$s",status);cmd.Parameters.AddWithValue("$id",taskId);cmd.ExecuteNonQuery();
        }
    }
    public List<WatchFolderRecord> WatchFolders()
    {
        using var con=Connect();using var cmd=con.CreateCommand();
        cmd.CommandText="SELECT id,path,workspace_id FROM watched_folders ORDER BY id";
        using var r=cmd.ExecuteReader();var list=new List<WatchFolderRecord>();
        while(r.Read())list.Add(new(r.GetInt64(0),r.GetString(1),r.GetInt64(2)));
        return list;
    }
    public void AddWatchFolder(string folder,long workspaceId)
    {
        var full=Path.GetFullPath(folder);
        if(!Directory.Exists(full))throw new DirectoryNotFoundException("The approved folder does not exist.");
        PathSafety.RejectReparsePoints(full);
        if(full.StartsWith(LocalPaths.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||full.Equals(LocalPaths.Root,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Cannot index internal application data as source documents.");
        lock(_writeGate)
        {
            using var con=Connect();using var cmd=con.CreateCommand();
            cmd.CommandText="INSERT OR IGNORE INTO watched_folders(path,workspace_id) VALUES($p,$w)";
            cmd.Parameters.AddWithValue("$p",full);cmd.Parameters.AddWithValue("$w",workspaceId);cmd.ExecuteNonQuery();
        }
    }
    public void RemoveWatchFolder(long id)
    {
        lock(_writeGate)
        {
            using var con=Connect();using var cmd=con.CreateCommand();
            cmd.CommandText="DELETE FROM watched_folders WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();
        }
    }
    public static readonly HashSet<string> ApprovedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {".txt",".md",".pdf",".docx",".xlsx",".pptx",".csv",".json",".xml",".log",".sql",".ps1",".cs",".yaml",".yml",".png",".jpg",".jpeg",".bmp",".tif",".tiff"};
    /// <summary>Safe bounded rescan; caller schedules subsequent scans and records failures.</summary>
    public async Task<FolderScanResult> ScanApprovedFoldersAsync(CancellationToken ct)
    {
        var imported=0; var skipped=0; var failed=0; var retired=0; var failures=new List<string>();
        foreach(var folder in WatchFolders())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // A previously approved directory can be replaced while the app is running.
                PathSafety.RejectReparsePoints(folder.Path);
                if (!Directory.Exists(folder.Path)) throw new DirectoryNotFoundException();
                var count = 0;
                var complete = true;
                // Observe every filename before attempting an import. A read failure is
                // not evidence that a document was deleted and must not retire it.
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in Directory.EnumerateFiles(folder.Path, "*", SearchOption.TopDirectoryOnly))
                {
                    ct.ThrowIfCancellationRequested();
                    if (++count > 5000) { complete = false; failed++; failures.Add("Folder exceeds 5,000-file scan limit; retirement skipped."); break; }
                    var canonical = Path.GetFullPath(path);
                    try
                    {
                        // Recheck folder ancestors before every ingestion; an approved
                        // directory may be replaced between scheduled scans.
                        PathSafety.RejectReparsePoints(folder.Path);
                        var file = new FileInfo(path);
                        // Unsafe, empty or oversized replacements must not leave an
                        // older revision advertised as current evidence.
                        if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || !ApprovedExtensions.Contains(file.Extension) ||
                            file.Length is <= 0 or > 25 * 1024 * 1024) { skipped++; continue; }
                        var result = await Task.Run(() => Import(path), ct);
                        seen.Add(canonical);
                        AssignDocument(result.DocumentId, folder.WorkspaceId);
                        if (result.Imported) imported++; else skipped++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // A temporary sharing/permission failure is not proof of
                        // deletion. Other failures cannot affirm that indexed text
                        // still matches the current physical document.
                        if ((ex is IOException && ex is not FileNotFoundException && ex is not DirectoryNotFoundException)
                            || ex is UnauthorizedAccessException)
                            seen.Add(canonical);
                        failed++;
                        if (failures.Count < 10) failures.Add(Path.GetFileName(path) + ": " + ex.GetType().Name);
                    }
                }
                // Only a complete successful directory enumeration can establish absence.
                // Keep old revisions for audit, but remove missing files from retrieval.
                if (complete)
                {
                    PathSafety.RejectReparsePoints(folder.Path);
                    if (!Directory.Exists(folder.Path)) throw new DirectoryNotFoundException();
                    retired += RetireMissingWatchedDocuments(folder.Path, seen, ct);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                failed++;
                if (failures.Count < 10) failures.Add("An approved folder is unavailable or unsafe: " + ex.GetType().Name);
            }
        }
        return new FolderScanResult(imported,skipped,failed,failures,retired);
    }

    private int RetireMissingWatchedDocuments(string folderPath, HashSet<string> seen, CancellationToken ct)
    {
        lock (_writeGate)
        {
            using var fileLock = FileOperationLock.Acquire("vault-files", ct);
            using var con = Connect();
            using var list = con.CreateCommand();
            list.CommandText = "SELECT id,original_path FROM documents WHERE active=1";
            var missing = new List<long>();
            using (var reader = list.ExecuteReader())
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    var original = reader.GetString(1);
                    if (string.Equals(Path.GetDirectoryName(original), folderPath, StringComparison.OrdinalIgnoreCase)
                        && !seen.Contains(original)) missing.Add(reader.GetInt64(0));
                }
            if (missing.Count == 0) return 0;
            ct.ThrowIfCancellationRequested();
            // No deletion is performed. Archived revisions remain in the local plaintext database.
            using var tx = con.BeginTransaction();
            using var retire = con.CreateCommand();
            retire.Transaction = tx;
            retire.CommandText = "UPDATE documents SET active=0 WHERE id=$id AND active=1";
            var id = retire.Parameters.Add("$id", SqliteType.Integer);
            var count = 0;
            foreach (var documentId in missing)
            {
                ct.ThrowIfCancellationRequested();
                id.Value = documentId;
                count += retire.ExecuteNonQuery();
            }
            tx.Commit();
            return count;
        }
    }

    /// <summary>Idempotent, workspace-scoped conversational learning. A repeated
    /// explicit memory request must never interrupt the conversation.</summary>
    public bool QueueMemoryIfNew(string text, string source, long workspaceId = 1)
    {
        text = ValidateMemoryText(text);
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Memory source is required.", nameof(source));
        lock (_writeGate)
        {
            using (var con = Connect())
            using (var check = con.CreateCommand())
            {
                RequireWorkspace(con, workspaceId);
                check.CommandText = """
                    SELECT 1 FROM memories WHERE approved=1 AND workspace_id=$w AND content=$c COLLATE NOCASE
                    UNION ALL
                    SELECT 1 FROM memory_proposals WHERE replaces_memory_id IS NULL AND workspace_id=$w AND content=$c COLLATE NOCASE
                    LIMIT 1
                    """;
                check.Parameters.AddWithValue("$w", workspaceId);
                check.Parameters.AddWithValue("$c", text);
                if (check.ExecuteScalar() is not null) return false;
            }
            // The same process-wide lock also guards ProposeMemory, so the check
            // and enqueue cannot race with another caller in this process.
            _ = ProposeMemory(text, source, workspaceId: workspaceId);
            return true;
        }
    }

    public long ProposeMemory(string text,string source,long? replacesMemoryId = null,long workspaceId = 1)
    {
        text = ValidateMemoryText(text);
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Memory source is required.", nameof(source));
        lock(_writeGate)
        {
            using var con=Connect();
            RequireWorkspace(con,workspaceId);
            if(replacesMemoryId is long replaceId)
            {
                using var verify=con.CreateCommand();
                verify.CommandText="SELECT content FROM memories WHERE id=$id AND approved=1 AND workspace_id=$w";
                verify.Parameters.AddWithValue("$id",replaceId);
                verify.Parameters.AddWithValue("$w",workspaceId);
                var old = verify.ExecuteScalar() as string ?? throw new InvalidDataException("The memory to correct is not active or does not exist.");
                if(string.Equals(old.Trim(),text,StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("The replacement does not change the existing memory.",nameof(text));
            }
            if (replacesMemoryId is null)
            {
                using var duplicate = con.CreateCommand();
                duplicate.CommandText = "SELECT id FROM memories WHERE approved=1 AND workspace_id=$w AND content=$c COLLATE NOCASE LIMIT 1";
                duplicate.Parameters.AddWithValue("$w",workspaceId);
                duplicate.Parameters.AddWithValue("$c",text);
                if (duplicate.ExecuteScalar() is not null)
                    throw new InvalidDataException("This fact is already an approved memory in this workspace.");
            }
            using var cmd=con.CreateCommand();
            cmd.CommandText="INSERT INTO memory_proposals(content,source,created_at,replaces_memory_id,workspace_id) VALUES($c,$s,$at,$prior,$w); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("$c",text);cmd.Parameters.AddWithValue("$s",source.Trim());
            cmd.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$prior",replacesMemoryId is long id ? id : DBNull.Value);
            cmd.Parameters.AddWithValue("$w",workspaceId);
            return (long)(cmd.ExecuteScalar()??throw new InvalidOperationException("Memory proposal failed."));
        }
    }
    public List<MemoryProposal> MemoryProposals(long? workspaceId = null)
    {
        using var con=Connect();using var cmd=con.CreateCommand();
        cmd.CommandText="SELECT id,content,source,replaces_memory_id FROM memory_proposals WHERE ($w IS NULL OR workspace_id=$w) ORDER BY id DESC";
        cmd.Parameters.AddWithValue("$w",workspaceId is long id ? id : DBNull.Value);
        using var r=cmd.ExecuteReader();var result=new List<MemoryProposal>();
        while(r.Read())result.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.IsDBNull(3)?null:r.GetInt64(3)));
        return result;
    }
    public void ResolveMemoryProposal(long id,bool approve)
    {
        lock(_writeGate)
        {
            using var con=Connect();using var tx=con.BeginTransaction();
            using var read=con.CreateCommand();read.Transaction=tx;
            read.CommandText="SELECT content,source,replaces_memory_id,workspace_id FROM memory_proposals WHERE id=$id";
            read.Parameters.AddWithValue("$id",id);
            string text,source;
            long? replaceId;
            long workspaceId;
            using(var reader=read.ExecuteReader())
            {
                if(!reader.Read())throw new InvalidDataException("Memory proposal no longer exists.");
                text=reader.GetString(0);source=reader.GetString(1);
                replaceId=reader.IsDBNull(2)?null:reader.GetInt64(2);
                workspaceId=reader.GetInt64(3);
            }
            if(approve)
            {
                using (var duplicate = con.CreateCommand())
                {
                    duplicate.Transaction = tx;
                    duplicate.CommandText = "SELECT id FROM memories WHERE approved=1 AND workspace_id=$w AND content=$c COLLATE NOCASE LIMIT 1";
                    duplicate.Parameters.AddWithValue("$w",workspaceId);
                    duplicate.Parameters.AddWithValue("$c",text);
                    if (duplicate.ExecuteScalar() is not null)
                        throw new InvalidDataException("This memory has already been approved. Reject the duplicate proposal.");
                }
                if(replaceId is long oldId)
                {
                    // Fail closed if a prior correction already superseded the selected fact.
                    using var check=con.CreateCommand();check.Transaction=tx;
                    check.CommandText="SELECT content FROM memories WHERE id=$id AND approved=1 AND workspace_id=$w";
                    check.Parameters.AddWithValue("$id",oldId);
                    check.Parameters.AddWithValue("$w",workspaceId);
                    var previous=check.ExecuteScalar() as string ?? throw new InvalidDataException("Correction target is no longer active. Review the current memory first.");
                    if(string.Equals(previous.Trim(),text,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Correction no longer changes the active memory.");
                }
                using var insert=con.CreateCommand();insert.Transaction=tx;
                insert.CommandText="INSERT INTO memories(content,source,created_at,approved,workspace_id) VALUES($c,$s,$at,1,$w); SELECT last_insert_rowid()";
                insert.Parameters.AddWithValue("$c",text);insert.Parameters.AddWithValue("$s",source);
                insert.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));
                insert.Parameters.AddWithValue("$w",workspaceId);
                var newId=(long)(insert.ExecuteScalar()??throw new InvalidOperationException("Memory approval failed."));
                if(replaceId is long supersededMemoryId)
                {
                    using var supersede=con.CreateCommand();supersede.Transaction=tx;
                    supersede.CommandText="UPDATE memories SET approved=0,superseded_by=$new WHERE id=$old AND approved=1 AND workspace_id=$w";
                    supersede.Parameters.AddWithValue("$new",newId);supersede.Parameters.AddWithValue("$old",supersededMemoryId);
                    supersede.Parameters.AddWithValue("$w",workspaceId);
                    if(supersede.ExecuteNonQuery()!=1)throw new InvalidDataException("The memory changed during approval.");
                }
            }
            using var remove=con.CreateCommand();remove.Transaction=tx;
            remove.CommandText="DELETE FROM memory_proposals WHERE id=$id";
            remove.Parameters.AddWithValue("$id",id);
            if(remove.ExecuteNonQuery()!=1)throw new InvalidDataException("Memory proposal no longer exists.");
            tx.Commit();
        }
    }
}
