using System.Globalization;
using Microsoft.Data.Sqlite;

namespace WorkIntelligence.Services;

public enum PolicyAuthority { UserNote=0,TeamProcedure=10,DepartmentPolicy=20,OrganizationPolicy=30 }
public sealed record ApprovedPolicy(long Id,string Topic,string Text,string Source,PolicyAuthority Authority,
    DateOnly EffectiveFrom,DateOnly? EffectiveUntil,DateOnly? ClosedOn,bool Approved,string ApprovalReason)
{
    public string DisplayName=>$"{Topic} — {Authority} — from {EffectiveFrom:yyyy-MM-dd}";
    public bool Applies(DateOnly date)=>Approved && date>=EffectiveFrom && (EffectiveUntil is null || date<EffectiveUntil) && (ClosedOn is null || date<ClosedOn);
}
public sealed class PolicyConflictException(IReadOnlyList<ApprovedPolicy> conflicts):InvalidOperationException("Overlapping approved policies require explicit review and replacement selection.")
{public IReadOnlyList<ApprovedPolicy> Conflicts {get;}=conflicts;}

public sealed partial class KnowledgeStore
{
    private static string Day(DateOnly value)=>value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    public List<ApprovedPolicy> Policies(long workspaceId,string? topic=null)
    {
        using var con=Connect();return ReadPolicies(con,null,workspaceId,topic);
    }
    private static List<ApprovedPolicy> ReadPolicies(SqliteConnection con,SqliteTransaction? tx,long workspace,string? topic)
    {
        using var cmd=con.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="""
            SELECT m.id,p.topic,m.content,m.source,p.authority,p.effective_from,p.effective_until,p.closed_on,m.approved,p.approval_reason
            FROM memory_policies p JOIN memories m ON m.id=p.memory_id
            WHERE m.workspace_id=$w AND ($topic IS NULL OR p.topic=$topic) ORDER BY p.effective_from DESC,m.id DESC
            """;
        cmd.Parameters.AddWithValue("$w",workspace);cmd.Parameters.AddWithValue("$topic",(object?)topic ?? DBNull.Value);
        using var r=cmd.ExecuteReader();var records=new List<ApprovedPolicy>();
        while(r.Read())records.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),(PolicyAuthority)r.GetInt32(4),
            DateOnly.ParseExact(r.GetString(5),"yyyy-MM-dd",CultureInfo.InvariantCulture),r.IsDBNull(6)?null:DateOnly.ParseExact(r.GetString(6),"yyyy-MM-dd",CultureInfo.InvariantCulture),
            r.IsDBNull(7)?null:DateOnly.ParseExact(r.GetString(7),"yyyy-MM-dd",CultureInfo.InvariantCulture),r.GetInt64(8)==1,r.GetString(9)));
        return records;
    }
    public long ApprovePolicy(long workspaceId,string topic,string text,string source,PolicyAuthority authority,
        DateOnly effectiveFrom,DateOnly? effectiveUntil,string reason,IReadOnlyList<long>? reviewedReplacements=null)
    {
        topic=topic.Trim().ToLowerInvariant();text=ValidateMemoryText(text);source=source.Trim();reason=reason.Trim();
        if(topic.Length is <1 or >120 || source.Length is <1 or >500 || reason.Length is <1 or >2000 || !Enum.IsDefined(authority)
            || effectiveUntil is not null && effectiveUntil<=effectiveFrom)throw new ArgumentException("Policy topic, source, authority, dates or approval reason is invalid.");
        lock(_writeGate)
        {
            using var con=Connect();RequireWorkspace(con,workspaceId);using var tx=con.BeginTransaction();
            var previous=ReadPolicies(con,tx,workspaceId,topic);
            DateOnly End(ApprovedPolicy p)=>new[]{p.EffectiveUntil ?? DateOnly.MaxValue,p.ClosedOn ?? DateOnly.MaxValue}.Min();
            var conflicts=previous.Where(p=>p.Approved && p.EffectiveFrom<(effectiveUntil ?? DateOnly.MaxValue) && effectiveFrom<End(p)).ToArray();
            var reviewed=(reviewedReplacements ?? []).ToHashSet();
            if(conflicts.Any(x=>!reviewed.Contains(x.Id)))throw new PolicyConflictException(conflicts);
            if(reviewed.Any(id=>!conflicts.Any(x=>x.Id==id)))throw new InvalidDataException("A reviewed policy changed or is outside the overlapping workspace/topic. Reload before approving.");
            using var insert=con.CreateCommand();insert.Transaction=tx;
            insert.CommandText="INSERT INTO memories(content,source,created_at,approved,workspace_id) VALUES($text,$source,$at,1,$w); SELECT last_insert_rowid()";
            var at=DateTimeOffset.UtcNow.ToString("O");
            insert.Parameters.AddWithValue("$text",text);insert.Parameters.AddWithValue("$source",$"User-reviewed {authority}; topic {topic}; effective {Day(effectiveFrom)}; source: {source}");
            insert.Parameters.AddWithValue("$at",at);insert.Parameters.AddWithValue("$w",workspaceId);
            var id=(long)(insert.ExecuteScalar() ?? throw new InvalidOperationException("Policy approval failed."));
            using var metadata=con.CreateCommand();metadata.Transaction=tx;
            metadata.CommandText="INSERT INTO memory_policies(memory_id,topic,authority,effective_from,effective_until,approval_reason,approved_at) VALUES($id,$topic,$authority,$from,$until,$reason,$at)";
            metadata.Parameters.AddWithValue("$id",id);metadata.Parameters.AddWithValue("$topic",topic);metadata.Parameters.AddWithValue("$authority",(int)authority);
            metadata.Parameters.AddWithValue("$from",Day(effectiveFrom));metadata.Parameters.AddWithValue("$until",effectiveUntil is null?DBNull.Value:Day(effectiveUntil.Value));
            metadata.Parameters.AddWithValue("$reason",reason);metadata.Parameters.AddWithValue("$at",at);metadata.ExecuteNonQuery();
            foreach(var old in conflicts)
            {
                using var close=con.CreateCommand();close.Transaction=tx;
                close.CommandText="UPDATE memory_policies SET closed_on=$from WHERE memory_id=$old; UPDATE memories SET superseded_by=$new,approved=CASE WHEN $from<= (SELECT effective_from FROM memory_policies WHERE memory_id=$old) THEN 0 ELSE approved END WHERE id=$old";
                close.Parameters.AddWithValue("$from",Day(effectiveFrom));close.Parameters.AddWithValue("$old",old.Id);close.Parameters.AddWithValue("$new",id);close.ExecuteNonQuery();
            }
            tx.Commit();return id;
        }
    }
}
