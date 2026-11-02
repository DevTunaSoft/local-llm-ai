using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using WorkIntelligence.Models;

namespace WorkIntelligence.Services;

public sealed partial class KnowledgeStore : IDisposable
{
    private readonly object _writeGate = new();
    public KnowledgeStore() { LocalPaths.Ensure(); Initialize(); }

    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = LocalPaths.Database,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        try
        {
        connection.Open();
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys=ON";
            pragma.ExecuteNonQuery();
            pragma.CommandText = "PRAGMA busy_timeout=5000";
            pragma.ExecuteNonQuery();
        }
        return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void Initialize()
    {
        using var connection = Connect();
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA user_version";
            var version = Convert.ToInt32(check.ExecuteScalar());
            if (version > 6)
                throw new InvalidDataException($"Vault database schema {version} is newer than this application supports. Refusing to modify it.");
        }
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
          PRAGMA journal_mode=DELETE;
          CREATE TABLE IF NOT EXISTS documents(id INTEGER PRIMARY KEY, name TEXT NOT NULL, original_path TEXT NOT NULL,
             sha256 TEXT NOT NULL, version INTEGER NOT NULL, imported_at TEXT NOT NULL, active INTEGER NOT NULL DEFAULT 1);
          CREATE INDEX IF NOT EXISTS ix_document_path ON documents(original_path, version);
          CREATE TABLE IF NOT EXISTS chunks(id INTEGER PRIMARY KEY, document_id INTEGER NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
             ordinal INTEGER NOT NULL, content TEXT NOT NULL);
          CREATE VIRTUAL TABLE IF NOT EXISTS chunks_fts USING fts5(content, content='chunks', content_rowid='id');
          CREATE TRIGGER IF NOT EXISTS chunks_ai AFTER INSERT ON chunks BEGIN
            INSERT INTO chunks_fts(rowid,content) VALUES (new.id,new.content); END;
          CREATE TRIGGER IF NOT EXISTS chunks_ad AFTER DELETE ON chunks BEGIN
            INSERT INTO chunks_fts(chunks_fts,rowid,content) VALUES ('delete',old.id,old.content); END;
          CREATE TRIGGER IF NOT EXISTS chunks_au AFTER UPDATE ON chunks BEGIN
            INSERT INTO chunks_fts(chunks_fts,rowid,content) VALUES ('delete',old.id,old.content);
            INSERT INTO chunks_fts(rowid,content) VALUES (new.id,new.content); END;
          CREATE TABLE IF NOT EXISTS memories(id INTEGER PRIMARY KEY, content TEXT NOT NULL, source TEXT NOT NULL,
            created_at TEXT NOT NULL, approved INTEGER NOT NULL DEFAULT 1);
          CREATE TABLE IF NOT EXISTS chats(id INTEGER PRIMARY KEY, title TEXT NOT NULL, created_at TEXT NOT NULL);
          CREATE TABLE IF NOT EXISTS messages(id INTEGER PRIMARY KEY, chat_id INTEGER NOT NULL REFERENCES chats(id) ON DELETE CASCADE,
            role TEXT NOT NULL CHECK(role IN ('system','user','assistant')), content TEXT NOT NULL, created_at TEXT NOT NULL);
          CREATE TABLE IF NOT EXISTS models(id INTEGER PRIMARY KEY, name TEXT NOT NULL, model_path TEXT NOT NULL UNIQUE,
            sha256 TEXT NOT NULL, added_at TEXT NOT NULL);
          CREATE TABLE IF NOT EXISTS chunk_vectors(
            chunk_id INTEGER PRIMARY KEY REFERENCES chunks(id) ON DELETE CASCADE,
            model_key TEXT NOT NULL, dims INTEGER NOT NULL, vector BLOB NOT NULL);
          CREATE TABLE IF NOT EXISTS workspaces(id INTEGER PRIMARY KEY,name TEXT NOT NULL UNIQUE);
          INSERT OR IGNORE INTO workspaces(id,name) VALUES(1,'General');
          CREATE TABLE IF NOT EXISTS workspace_documents(workspace_id INTEGER NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
            document_id INTEGER NOT NULL REFERENCES documents(id) ON DELETE CASCADE,PRIMARY KEY(workspace_id,document_id));
          CREATE TABLE IF NOT EXISTS work_tasks(id INTEGER PRIMARY KEY,workspace_id INTEGER NOT NULL REFERENCES workspaces(id),
            title TEXT NOT NULL,details TEXT NOT NULL,status TEXT NOT NULL,created_at TEXT NOT NULL);
          CREATE TABLE IF NOT EXISTS watched_folders(id INTEGER PRIMARY KEY,path TEXT NOT NULL UNIQUE,
            workspace_id INTEGER NOT NULL REFERENCES workspaces(id));
          CREATE TABLE IF NOT EXISTS memory_proposals(id INTEGER PRIMARY KEY,content TEXT NOT NULL,source TEXT NOT NULL,created_at TEXT NOT NULL);
          """;
        cmd.ExecuteNonQuery();
        // Schema 4: a searchable, revision-safe approval ledger. Transactional upgrade
        // retains historical schema upgrades and never rewrites memory text.
        using (var current = connection.CreateCommand())
        {
            current.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(current.ExecuteScalar()) < 4)
            {
                using var migration = connection.BeginTransaction();
                using var upgrade = connection.CreateCommand();
                upgrade.Transaction = migration;
                upgrade.CommandText = """
                    ALTER TABLE memories ADD COLUMN superseded_by INTEGER REFERENCES memories(id) ON DELETE SET NULL;
                    ALTER TABLE memory_proposals ADD COLUMN replaces_memory_id INTEGER REFERENCES memories(id) ON DELETE CASCADE;
                    CREATE VIRTUAL TABLE IF NOT EXISTS memories_fts USING fts5(content, content='memories', content_rowid='id', tokenize='porter unicode61');
                    CREATE TRIGGER IF NOT EXISTS memories_ai AFTER INSERT ON memories BEGIN
                      INSERT INTO memories_fts(rowid,content) VALUES(new.id,new.content); END;
                    CREATE TRIGGER IF NOT EXISTS memories_ad AFTER DELETE ON memories BEGIN
                      INSERT INTO memories_fts(memories_fts,rowid,content) VALUES('delete',old.id,old.content); END;
                    CREATE TRIGGER IF NOT EXISTS memories_au AFTER UPDATE OF content ON memories BEGIN
                      INSERT INTO memories_fts(memories_fts,rowid,content) VALUES('delete',old.id,old.content);
                      INSERT INTO memories_fts(rowid,content) VALUES(new.id,new.content); END;
                    INSERT INTO memories_fts(memories_fts) VALUES('rebuild');
                    PRAGMA user_version=4;
                    """;
                upgrade.ExecuteNonQuery();
                migration.Commit();
            }
        }
        // Schema 5: logical workspace scopes on chats and memory. Existing knowledge
        // remains in the General workspace. An existing plaintext database is preserved.
        using (var current = connection.CreateCommand())
        {
            current.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(current.ExecuteScalar()) < 5)
            {
                using var tx = connection.BeginTransaction();
                using var migration = connection.CreateCommand();
                migration.Transaction = tx;
                migration.CommandText = """
                    ALTER TABLE chats ADD COLUMN workspace_id INTEGER NOT NULL DEFAULT 1;
                    ALTER TABLE memories ADD COLUMN workspace_id INTEGER NOT NULL DEFAULT 1;
                    ALTER TABLE memory_proposals ADD COLUMN workspace_id INTEGER NOT NULL DEFAULT 1;
                    CREATE INDEX IF NOT EXISTS ix_chats_workspace ON chats(workspace_id,id);
                    CREATE INDEX IF NOT EXISTS ix_memories_workspace ON memories(workspace_id,approved);
                    -- Preserve legacy imported documents without an explicit workspace.
                    INSERT OR IGNORE INTO workspace_documents(workspace_id,document_id)
                      SELECT 1,d.id FROM documents d WHERE NOT EXISTS
                        (SELECT 1 FROM workspace_documents wd WHERE wd.document_id=d.id);
                    PRAGMA user_version=5;
                    """;
                migration.ExecuteNonQuery();
                tx.Commit();
            }
        }
        // Schema 6: local, workspace-filtered retrieval of prior user-authored
        // conversation messages. This is contextual recall, not approved policy.
        using (var current = connection.CreateCommand())
        {
            current.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(current.ExecuteScalar()) < 6)
            {
                using var tx = connection.BeginTransaction();
                using var recallUpgrade = connection.CreateCommand();
                recallUpgrade.Transaction = tx;
                recallUpgrade.CommandText = """
                    CREATE VIRTUAL TABLE IF NOT EXISTS messages_fts USING fts5(
                        content, content='messages', content_rowid='id', tokenize='porter unicode61');
                    CREATE TRIGGER IF NOT EXISTS messages_ai AFTER INSERT ON messages BEGIN
                      INSERT INTO messages_fts(rowid,content) VALUES(new.id,new.content); END;
                    CREATE TRIGGER IF NOT EXISTS messages_ad AFTER DELETE ON messages BEGIN
                      INSERT INTO messages_fts(messages_fts,rowid,content) VALUES('delete',old.id,old.content); END;
                    CREATE TRIGGER IF NOT EXISTS messages_au AFTER UPDATE OF content ON messages BEGIN
                      INSERT INTO messages_fts(messages_fts,rowid,content) VALUES('delete',old.id,old.content);
                      INSERT INTO messages_fts(rowid,content) VALUES(new.id,new.content); END;
                    INSERT INTO messages_fts(messages_fts) VALUES('rebuild');
                    PRAGMA user_version=6;
                    """;
                recallUpgrade.ExecuteNonQuery();
                tx.Commit();
            }
        }
        using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT count(*) FROM sqlite_master";
        _ = verify.ExecuteScalar();
    }
    public static IEnumerable<string> Chunk(string text, int size = 1400, int overlap = 160)
    {
        if (size <= 0 || overlap < 0 || overlap >= size) throw new ArgumentOutOfRangeException(nameof(size));
        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        for (var start = 0; start < normalized.Length; start += size - overlap)
        {
            var length = Math.Min(size, normalized.Length - start);
            yield return normalized.Substring(start, length);
            if (start + length == normalized.Length) yield break;
        }
    }

    public ImportResult Import(string path)
    {
        var full = Path.GetFullPath(path);
        var fi = new FileInfo(full);
        if (!fi.Exists || fi.Length is <= 0 or > 25 * 1024 * 1024) throw new InvalidDataException("File missing, empty, or exceeds 25 MiB import limit.");
        var input = File.ReadAllBytes(full);
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(input));
            var extracted = DocumentExtractor.Extract(full, input);
            if (string.IsNullOrWhiteSpace(extracted)) throw new InvalidDataException("Document contains no extractable text; scanned/image-only documents require OCR.");
            var chunks = Chunk(extracted).ToArray();
            lock (_writeGate)
            {
                using var fileLock = FileOperationLock.Acquire("vault-files");
                using var con = Connect();
                using var existing = con.CreateCommand();
                existing.CommandText = "SELECT id,sha256,version,active FROM documents WHERE original_path=$p ORDER BY version DESC LIMIT 1";
                existing.Parameters.AddWithValue("$p", full);
                long priorId = 0;
                int version = 1;
                using (var reader = existing.ExecuteReader())
                    if (reader.Read())
                    {
                        priorId = reader.GetInt64(0);
                        if (reader.GetInt64(3) == 1 && string.Equals(reader.GetString(1), hash, StringComparison.OrdinalIgnoreCase))
                            return new ImportResult(false, priorId, reader.GetInt32(2), chunks.Length, "Unchanged document skipped.");
                        version = reader.GetInt32(2) + 1;
                    }
                var blobPath = "";
                var blobCreated = false;
                try
                {
                    using var tx = con.BeginTransaction();
                    using var doc = con.CreateCommand();
                    doc.Transaction = tx;
                    doc.CommandText = "INSERT INTO documents(name,original_path,sha256,version,imported_at,active) VALUES($n,$p,$h,$v,$d,1); SELECT last_insert_rowid();";
                    doc.Parameters.AddWithValue("$n", Path.GetFileName(full));
                    doc.Parameters.AddWithValue("$p", full);
                    doc.Parameters.AddWithValue("$h", hash);
                    doc.Parameters.AddWithValue("$v", version);
                    doc.Parameters.AddWithValue("$d", DateTimeOffset.UtcNow.ToString("O"));
                    var id = (long)(doc.ExecuteScalar() ?? throw new InvalidOperationException("Failed to create document."));
                    blobPath = Path.Combine(LocalPaths.Blobs, $"{id}.bin");
                    using (var file = new FileStream(blobPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        blobCreated = true;
                        file.Write(input);
                        file.Flush(true);
                    }
                    using var add = con.CreateCommand();
                    add.Transaction = tx;
                    add.CommandText = "INSERT INTO chunks(document_id,ordinal,content) VALUES($id,$i,$c)";
                    add.Parameters.AddWithValue("$id", id);
                    var indexParam = add.Parameters.Add("$i", SqliteType.Integer);
                    var contentParam = add.Parameters.Add("$c", SqliteType.Text);
                    for (var index = 0; index < chunks.Length; index++)
                    {
                        indexParam.Value = index;
                        contentParam.Value = chunks[index];
                        add.ExecuteNonQuery();
                    }
                    if (priorId != 0)
                    {
                        // Workspace assignment follows the source across revisions.
                        using var assign = con.CreateCommand();
                        assign.Transaction = tx;
                        assign.CommandText = """
                            INSERT OR IGNORE INTO workspace_documents(workspace_id,document_id)
                            SELECT workspace_id,$id FROM workspace_documents WHERE document_id=$prior
                            """;
                        assign.Parameters.AddWithValue("$id", id);
                        assign.Parameters.AddWithValue("$prior", priorId);
                        assign.ExecuteNonQuery();
                        using var disable = con.CreateCommand();
                        disable.Transaction = tx;
                        disable.CommandText = "UPDATE documents SET active=0 WHERE original_path=$p AND id<>$id";
                        disable.Parameters.AddWithValue("$p", full);
                        disable.Parameters.AddWithValue("$id", id);
                        disable.ExecuteNonQuery();
                    }
                    tx.Commit();
                    return new ImportResult(true, id, version, chunks.Length, "Imported successfully.");
                }
                catch { if (blobCreated && File.Exists(blobPath)) File.Delete(blobPath); throw; }
            }
        }
        finally { CryptographicOperations.ZeroMemory(input); }
    }
    public List<DocumentRecord> Documents()
    {
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id,name,original_path,sha256,version,imported_at,active FROM documents ORDER BY imported_at DESC";
        using var r = cmd.ExecuteReader();
        var output = new List<DocumentRecord>();
        while (r.Read()) output.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetInt32(4),DateTimeOffset.Parse(r.GetString(5)),r.GetInt64(6)==1));
        return output;
    }
    public void DeleteDocument(long id)
    {
        lock (_writeGate)
        {
            using var fileLock = FileOperationLock.Acquire("vault-files");
            using var con = Connect();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM documents WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            var path = Path.Combine(LocalPaths.Blobs, $"{id}.bin");
            if (File.Exists(path)) File.Delete(path);
        }
    }
    public List<ChunkHit> Search(string query, int limit = 8, long? workspaceId = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var tokens = Regex.Matches(query, @"[\p{L}\p{N}_]{2,40}").Select(m => m.Value.ToLowerInvariant()).Distinct().Take(12).ToArray();
        if (tokens.Length == 0) return [];
        var fts = string.Join(" OR ", tokens.Select(s => $"\"{s}\""));
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT c.document_id,d.name,d.version,c.ordinal,c.content,bm25(chunks_fts)
            FROM chunks_fts JOIN chunks c ON c.id=chunks_fts.rowid
            JOIN documents d ON d.id=c.document_id
            WHERE chunks_fts MATCH $query AND d.active=1
              AND ($workspace IS NULL OR EXISTS
                (SELECT 1 FROM workspace_documents wd
                 WHERE wd.document_id=d.id AND wd.workspace_id=$workspace))
            ORDER BY bm25(chunks_fts) LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$query", fts);
        cmd.Parameters.AddWithValue("$workspace", workspaceId is long id ? id : DBNull.Value);
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit,1,20));
        using var reader = cmd.ExecuteReader(); var hits = new List<ChunkHit>();
        while (reader.Read()) hits.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetString(4),reader.GetDouble(5)));
        return hits;
    }
    // Semantic vectors are persisted in the plaintext SQLite database.
    // This is intentionally a linear scan for the initial single-user build; use an
    // audited native ANN index only after measured corpus size justifies its complexity.
    public async Task<int> IndexSemanticAsync(EmbeddingRuntime embedder, CancellationToken ct)
    {
        var key=embedder.ModelKey ?? throw new InvalidOperationException("Start the local embedding engine first.");
        var pending = new List<(long id,string content)>();
        using(var con=Connect())
        using(var cmd=con.CreateCommand())
        {
            cmd.CommandText="""
                SELECT c.id,c.content FROM chunks c JOIN documents d ON d.id=c.document_id
                LEFT JOIN chunk_vectors v ON v.chunk_id=c.id AND v.model_key=$model
                WHERE d.active=1 AND v.chunk_id IS NULL ORDER BY c.id
                """;
            cmd.Parameters.AddWithValue("$model",key);
            using var reader=cmd.ExecuteReader();
            while(reader.Read()) pending.Add((reader.GetInt64(0),reader.GetString(1)));
        }
        var completed=0;
        foreach(var item in pending)
        {
            ct.ThrowIfCancellationRequested();
            var vector=await embedder.EmbedAsync(item.content,ct);
            var bytes=SerializeVector(vector);
            lock(_writeGate)
            {
                using var con=Connect();using var cmd=con.CreateCommand();
                cmd.CommandText="""
                    INSERT INTO chunk_vectors(chunk_id,model_key,dims,vector)
                    SELECT id,$m,$d,$v FROM chunks WHERE id=$id
                    ON CONFLICT(chunk_id) DO UPDATE SET model_key=$m,dims=$d,vector=$v
                    """;
                cmd.Parameters.AddWithValue("$id",item.id);
                cmd.Parameters.AddWithValue("$m",key);
                cmd.Parameters.AddWithValue("$d",vector.Length);
                cmd.Parameters.AddWithValue("$v",bytes);
                if(cmd.ExecuteNonQuery()>0) completed++;
            }
        }
        return completed;
    }

    public async Task<List<ChunkHit>> SearchHybridAsync(string query, EmbeddingRuntime? embedder, int limit=8,
        CancellationToken cancellationToken=default, long? workspaceId = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        cancellationToken.ThrowIfCancellationRequested();
        limit=Math.Clamp(limit,1,20);
        var lexical=Search(query,Math.Min(20,limit*2),workspaceId);
        if(embedder is null || !embedder.IsRunning || embedder.ModelKey is null) return lexical.Take(limit).ToList();
        var vector=await embedder.EmbedAsync(query,cancellationToken);
        var semantic = new List<(ChunkHit hit,double sim)>();
        using(var con=Connect())
        using(var cmd=con.CreateCommand())
        {
            cmd.CommandText="""
              SELECT c.document_id,d.name,d.version,c.ordinal,c.content,v.vector,v.dims
              FROM chunk_vectors v JOIN chunks c ON c.id=v.chunk_id
              JOIN documents d ON d.id=c.document_id
              WHERE d.active=1 AND v.model_key=$model AND v.dims=$dims
                AND ($workspace IS NULL OR EXISTS
                  (SELECT 1 FROM workspace_documents wd
                   WHERE wd.document_id=d.id AND wd.workspace_id=$workspace))
              """;
            cmd.Parameters.AddWithValue("$model",embedder.ModelKey);
            cmd.Parameters.AddWithValue("$dims",vector.Length);
            cmd.Parameters.AddWithValue("$workspace",workspaceId is long id ? id : DBNull.Value);
            using var reader=cmd.ExecuteReader();
            while(reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate=DeserializeVector((byte[])reader[5],reader.GetInt32(6));
                double score=0;
                for(var i=0;i<vector.Length;i++)score+=(double)vector[i]*candidate[i];
                semantic.Add((new ChunkHit(reader.GetInt64(0),reader.GetString(1),reader.GetInt32(2),
                    reader.GetInt32(3),reader.GetString(4),-score),score));
            }
        }
        var ranked=semantic.OrderByDescending(s=>s.sim).Take(20).Select(s=>s.hit).ToList();
        var results=new Dictionary<(long,int), (ChunkHit hit,double score)>();
        for(var i=0;i<lexical.Count;i++)
        {
            var hit=lexical[i];var key=(hit.DocumentId,hit.Ordinal);
            results[key]=(hit,1d/(60+i+1));
        }
        for(var i=0;i<ranked.Count;i++)
        {
            var hit=ranked[i];var key=(hit.DocumentId,hit.Ordinal);
            var score=1d/(60+i+1);
            if(results.TryGetValue(key,out var existing)) results[key]=(existing.hit,existing.score+score);
            else results[key]=(hit,score);
        }
        return results.Values.OrderByDescending(r=>r.score).Take(limit).Select(r=>r.hit).ToList();
    }

    private static byte[] SerializeVector(float[] values)
    {
        var bytes=new byte[checked(values.Length*4)];
        for(var i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4,4),values[i]);
        return bytes;
    }
    private static float[] DeserializeVector(byte[] bytes,int dims)
    {
        if(dims is <16 or >8192 || bytes.Length!=dims*4)throw new InvalidDataException("Corrupt semantic vector.");
        var output=new float[dims];
        for(var i=0;i<dims;i++)
        {
            output[i]=BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i*4,4));
            if(!float.IsFinite(output[i]))throw new InvalidDataException("Corrupt semantic vector component.");
        }
        return output;
    }

    public long AddMemory(string content, string source, long workspaceId = 1)
    {
        content = ValidateMemoryText(content);
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Memory source is required.", nameof(source));
        lock (_writeGate)
        {
            using var con = Connect();
            RequireWorkspace(con,workspaceId);
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT id FROM memories WHERE approved=1 AND workspace_id=$w AND content=$c COLLATE NOCASE LIMIT 1";
            cmd.Parameters.AddWithValue("$c", content);
            cmd.Parameters.AddWithValue("$w", workspaceId);
            if (cmd.ExecuteScalar() is long existing) return existing;
            cmd.CommandText = "INSERT INTO memories(content,source,created_at,approved,workspace_id) VALUES($c,$s,$d,1,$w); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("$s", source.Trim());
            cmd.Parameters.AddWithValue("$d", DateTimeOffset.UtcNow.ToString("O"));
            return (long)(cmd.ExecuteScalar() ?? throw new InvalidOperationException("Memory creation failed."));
        }
    }

    internal static string ValidateMemoryText(string? content)
    {
        var text = content?.Trim() ?? "";
        if (text.Length is < 1 or > 8000) throw new ArgumentException("Memory must contain 1–8000 characters.", nameof(content));
        return text;
    }

    public List<MemoryRecord> Memories(long? workspaceId = null)
    {
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id,content,source,created_at FROM memories WHERE approved=1 AND ($w IS NULL OR workspace_id=$w) ORDER BY id DESC";
        cmd.Parameters.AddWithValue("$w", workspaceId is long id ? id : DBNull.Value);
        using var r = cmd.ExecuteReader(); var records = new List<MemoryRecord>();
        while (r.Read()) records.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),DateTimeOffset.Parse(r.GetString(3))));
        return records;
    }

    /// <summary>Retrieve only query-relevant approved knowledge. Superseded memories
    /// remain in audit history, but can never appear in generation context.</summary>
    public List<MemoryRecord> SearchMemories(string query, int limit = 8, long? workspaceId = null)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var terms = Regex.Matches(query, @"[\p{L}\p{N}_]{2,40}")
            .Select(match => match.Value.ToLowerInvariant())
            .Where(term => !MemoryStopWords.Contains(term))
            .Distinct().Take(16).ToArray();
        if (terms.Length == 0) return [];
        var fts = string.Join(" OR ", terms.Select(term => $"\"{term}\""));
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT m.id,m.content,m.source,m.created_at
            FROM memories_fts JOIN memories m ON m.id=memories_fts.rowid
            WHERE memories_fts MATCH $q AND m.approved=1 AND ($w IS NULL OR m.workspace_id=$w)
            ORDER BY rank, m.id DESC LIMIT $n
            """;
        cmd.Parameters.AddWithValue("$q", fts);
        cmd.Parameters.AddWithValue("$w", workspaceId is long id ? id : DBNull.Value);
        cmd.Parameters.AddWithValue("$n", Math.Clamp(limit,1,20));
        using var reader = cmd.ExecuteReader();
        var results = new List<MemoryRecord>();
        while(reader.Read()) results.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),DateTimeOffset.Parse(reader.GetString(3))));
        return results;
    }

    private static readonly HashSet<string> MemoryStopWords = new(StringComparer.OrdinalIgnoreCase)
    { "the", "and", "for", "are", "with", "what", "when", "where", "who", "how", "why", "does", "did", "can", "could", "would", "should", "have", "has", "had", "our", "your", "you", "please", "about", "from", "this", "that", "there", "their", "they", "them", "was", "were", "will", "into", "some", "any", "use", "using", "tell", "show", "give", "need", "make", "me", "my", "we", "is", "it", "to", "of", "in", "on", "at", "an", "as", "or", "be", "by", "do", "a", "i" };

    public List<MemoryHistoryRecord> MemoryHistory(int limit = 100, long? workspaceId = null)
    {
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id,content,source,created_at,approved,superseded_by FROM memories WHERE ($w IS NULL OR workspace_id=$w) ORDER BY id DESC LIMIT $n";
        cmd.Parameters.AddWithValue("$w", workspaceId is long id ? id : DBNull.Value);
        cmd.Parameters.AddWithValue("$n", Math.Clamp(limit,1,500));
        using var r = cmd.ExecuteReader(); var list = new List<MemoryHistoryRecord>();
        while(r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),DateTimeOffset.Parse(r.GetString(3)),r.GetInt64(4)==1,r.IsDBNull(5)?null:r.GetInt64(5)));
        return list;
    }

    public void DeleteMemory(long id)
    {
        lock(_writeGate)
        {
            using var con = Connect(); using var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM memories WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }
    public long CreateChat(string title, long workspaceId = 1)
    {
        using var con = Connect();
        RequireWorkspace(con,workspaceId);
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT INTO chats(title,created_at,workspace_id) VALUES($t,$d,$w); SELECT last_insert_rowid()";
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$w", workspaceId);
        cmd.Parameters.AddWithValue("$d", DateTimeOffset.UtcNow.ToString("O"));
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }
    public void RenameChat(long chatId, string title, long workspaceId)
    {
        title = title?.Trim() ?? throw new ArgumentNullException(nameof(title));
        if (title.Length is < 1 or > 120 || title.Any(char.IsControl))
            throw new ArgumentException("Chat title must be 1–120 printable characters.", nameof(title));
        using var con = Connect();
        RequireWorkspace(con, workspaceId);
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE chats SET title=$t WHERE id=$id AND workspace_id=$w";
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$id", chatId);
        cmd.Parameters.AddWithValue("$w", workspaceId);
        if (cmd.ExecuteNonQuery() != 1)
            throw new InvalidDataException("Conversation not found in the selected workspace.");
    }

    public List<ChatRecord> Chats(long? workspaceId = null)
    {
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id,title,created_at FROM chats WHERE ($w IS NULL OR workspace_id=$w) ORDER BY id DESC";
        cmd.Parameters.AddWithValue("$w", workspaceId is long id ? id : DBNull.Value);
        using var r = cmd.ExecuteReader(); var list = new List<ChatRecord>();
        while (r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),DateTimeOffset.Parse(r.GetString(2))));
        return list;
    }
    public List<ChatMessage> Messages(long chatId, long? expectedWorkspaceId = null)
    {
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT m.id,m.chat_id,m.role,m.content,m.created_at FROM messages m
            JOIN chats h ON h.id=m.chat_id WHERE m.chat_id=$c
              AND ($w IS NULL OR h.workspace_id=$w) ORDER BY m.id
            """;
        cmd.Parameters.AddWithValue("$w", expectedWorkspaceId is long id ? id : DBNull.Value);
        cmd.Parameters.AddWithValue("$c",chatId);
        using var r = cmd.ExecuteReader(); var list = new List<ChatMessage>();
        while (r.Read()) list.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetString(3),DateTimeOffset.Parse(r.GetString(4))));
        return list;
    }
    /// <summary>Search earlier user-authored statements within one workspace only.
    /// Results are contextual evidence, never an automatically approved memory.</summary>
    public List<ConversationRecallRecord> RecallEarlierConversations(string query, long workspaceId,
        long excludeChatId, int limit = 4)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var terms = Regex.Matches(query, @"[\p{L}\p{N}_]{2,40}")
            .Select(match => match.Value.ToLowerInvariant())
            .Where(term => !MemoryStopWords.Contains(term))
            .Distinct().Take(12).ToArray();
        if (terms.Length == 0) return [];
        var fts = string.Join(" OR ", terms.Select(term => $"\"{term}\""));
        using var con = Connect();
        RequireWorkspace(con, workspaceId);
        using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT m.chat_id,m.id,m.content FROM messages_fts
            JOIN messages m ON m.id=messages_fts.rowid
            JOIN chats h ON h.id=m.chat_id
            WHERE messages_fts MATCH $q AND m.role='user'
              AND h.workspace_id=$w AND h.id<>$exclude
            ORDER BY bm25(messages_fts), m.id DESC LIMIT $n
            """;
        cmd.Parameters.AddWithValue("$q", fts);
        cmd.Parameters.AddWithValue("$w", workspaceId);
        cmd.Parameters.AddWithValue("$exclude", excludeChatId);
        cmd.Parameters.AddWithValue("$n", Math.Clamp(limit, 1, 8));
        using var reader = cmd.ExecuteReader();
        var results = new List<ConversationRecallRecord>();
        while (reader.Read()) results.Add(new(reader.GetInt64(0),reader.GetInt64(1),reader.GetString(2)));
        return results;
    }

    public void AddMessage(long chatId,string role,string content, long? expectedWorkspaceId = null)
    {
        if (role is not ("user" or "assistant" or "system")) throw new ArgumentException("Invalid chat role.");
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText = """
            INSERT INTO messages(chat_id,role,content,created_at)
            SELECT id,$r,$t,$d FROM chats WHERE id=$c
              AND ($w IS NULL OR workspace_id=$w)
            """;
        cmd.Parameters.AddWithValue("$w", expectedWorkspaceId is long id ? id : DBNull.Value);
        cmd.Parameters.AddWithValue("$c",chatId); cmd.Parameters.AddWithValue("$r",role);
        cmd.Parameters.AddWithValue("$t",content); cmd.Parameters.AddWithValue("$d",DateTimeOffset.UtcNow.ToString("O"));
        if (cmd.ExecuteNonQuery() != 1) throw new InvalidDataException("Chat is missing or belongs to another workspace.");
    }
    public void DeleteChat(long chatId)
    {
        using var con = Connect(); using var cmd = con.CreateCommand();
        cmd.CommandText="DELETE FROM chats WHERE id=$id";cmd.Parameters.AddWithValue("$id",chatId);cmd.ExecuteNonQuery();
    }
    public ModelRecord ImportModel(string path)
    {
        var full=Path.GetFullPath(path);
        using var stream=File.OpenRead(full);
        if (stream.Length < 1024) throw new InvalidDataException("Model file is unexpectedly small.");
        Span<byte> header=stackalloc byte[4]; stream.ReadExactly(header);
        if (!header.SequenceEqual("GGUF"u8)) throw new InvalidDataException("Invalid GGUF model signature.");
        stream.Position=0;
        var sha=Convert.ToHexString(SHA256.HashData(stream));
        using var con=Connect();using var cmd=con.CreateCommand();
        cmd.CommandText="INSERT INTO models(name,model_path,sha256,added_at) VALUES($n,$p,$h,$d) ON CONFLICT(model_path) DO UPDATE SET sha256=$h; SELECT id FROM models WHERE model_path=$p";
        cmd.Parameters.AddWithValue("$n",Path.GetFileNameWithoutExtension(full));
        cmd.Parameters.AddWithValue("$p",full);cmd.Parameters.AddWithValue("$h",sha);
        cmd.Parameters.AddWithValue("$d",DateTimeOffset.UtcNow.ToString("O"));
        return new((long)(cmd.ExecuteScalar()??0L),Path.GetFileNameWithoutExtension(full),full,sha,DateTimeOffset.UtcNow);
    }
    public List<ModelRecord> Models()
    {
        using var con=Connect();using var cmd=con.CreateCommand();
        cmd.CommandText="SELECT id,name,model_path,sha256,added_at FROM models ORDER BY id DESC";
        using var r=cmd.ExecuteReader();var result=new List<ModelRecord>();
        while(r.Read())result.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),DateTimeOffset.Parse(r.GetString(4))));
        return result;
    }
    public void DeleteModelRecord(long id)
    {
        using var con=Connect();using var cmd=con.CreateCommand();
        cmd.CommandText="DELETE FROM models WHERE id=$id";cmd.Parameters.AddWithValue("$id",id);cmd.ExecuteNonQuery();
    }
    public void BackupDatabase(string destination)
    {
        // Standard SQLite online backup maintains consistency without stopping readers.
        using var source=Connect();
        using var target=new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource=destination,Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false }.ToString());
        target.Open(); source.BackupDatabase(target);
        using var cmd=target.CreateCommand();cmd.CommandText="PRAGMA integrity_check";
        if (!string.Equals(cmd.ExecuteScalar()?.ToString(),"ok",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Backup database failed integrity check.");
    }
    // A consistent snapshot includes the database and all imported raw document attachments.
    // Writers that modify blob files must share the _writeGate with this operation.
    public void CreateBackupSnapshot(string payload, CancellationToken ct)
    {
        lock (_writeGate)
        {
            using var fileLock = FileOperationLock.Acquire("vault-files", ct);
            Directory.CreateDirectory(payload);
            BackupDatabase(Path.Combine(payload, "knowledge.db"));
            ct.ThrowIfCancellationRequested();
            var blobTarget = Path.Combine(payload, "Blobs");
            Directory.CreateDirectory(blobTarget);
            foreach (var file in Directory.EnumerateFiles(LocalPaths.Blobs, "*.bin"))
            {
                ct.ThrowIfCancellationRequested();
                File.Copy(file, Path.Combine(blobTarget, Path.GetFileName(file)));
            }
        }
    }

    public string CheckDatabase()
    {
        using var con=Connect();using var cmd=con.CreateCommand();cmd.CommandText="PRAGMA integrity_check";
        return cmd.ExecuteScalar()?.ToString() ?? "Unknown";
    }
    public void Dispose() { /* Connections are short-lived and disposed per operation. */ }
}
