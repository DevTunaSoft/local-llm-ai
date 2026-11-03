namespace WorkIntelligence.Models;

public sealed record DocumentRecord(long Id, string Name, string OriginalPath, string Hash, int Version, DateTimeOffset ImportedAt, bool Active)
{public string DisplayName=>$"{Name} — D{Id}, v{Version} — {(Active?"current":"archived")}";}

public sealed record ChunkHit(long DocumentId, string Name, int Version, int Ordinal, string Content, double Rank);
public sealed record MemoryRecord(long Id, string Text, string Source, DateTimeOffset CreatedAt);
public sealed record ChatRecord(long Id, string Title, DateTimeOffset CreatedAt);
public sealed record ChatMessage(long Id, long ChatId, string Role, string Content, DateTimeOffset CreatedAt);
public sealed record ModelRecord(long Id, string DisplayName, string Path, string Sha256, DateTimeOffset AddedAt);
public sealed record ImportResult(bool Imported, long DocumentId, int Version, int Chunks, string Explanation);
public sealed record WorkspaceRecord(long Id,string Name);
public sealed record WorkTaskRecord(long Id,long WorkspaceId,string Title,string Details,string Status,DateTimeOffset CreatedAt);
public sealed record WatchFolderRecord(long Id,string Path,long WorkspaceId);
public sealed record FolderScanResult(int Imported,int Skipped,int Failed,IReadOnlyList<string> Errors,int Retired = 0);
public sealed record ConversationRecallRecord(long ChatId,long MessageId,string Text);
public sealed record MemoryProposal(long Id,string Text,string Source,long? ReplacesMemoryId = null);
public sealed record MemoryHistoryRecord(long Id,string Text,string Source,DateTimeOffset CreatedAt,bool Active,long? SupersededBy);

public sealed record WorkRecord(long Id,long WorkspaceId,string Kind,string Title,string Details,string Owner,
    string? DueDate,string Status,long? RelatedId,int Revision,string UpdatedAt);
public sealed record WorkRecordRevision(int Revision,string Reason,string CreatedAt,WorkRecord Record);
