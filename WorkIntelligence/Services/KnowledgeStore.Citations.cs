using System.Globalization;
using System.Text.RegularExpressions;
using WorkIntelligence.Models;

namespace WorkIntelligence.Services;

public sealed partial class KnowledgeStore
{
    public (ChunkHit Source,bool Current) ResolveCitation(string citation,long workspaceId)
    {
        var match=Regex.Match(citation.Trim(),@"^(?:\[source\s+)?D([1-9][0-9]{0,18})-V([1-9][0-9]{0,8})-S([1-9][0-9]{0,8})(?:\])?$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        if(!match.Success || !long.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out var document)
            || !int.TryParse(match.Groups[2].Value,out var version)||!int.TryParse(match.Groups[3].Value,out var segment))throw new ArgumentException("Use a stable citation such as D12-V2-S3.");
        using var con=Connect();using var cmd=con.CreateCommand();
        cmd.CommandText="""
            SELECT d.name,c.content,d.active FROM chunks c JOIN documents d ON d.id=c.document_id
            JOIN workspace_documents wd ON wd.document_id=d.id
            WHERE d.id=$id AND d.version=$version AND c.ordinal=$ordinal AND wd.workspace_id=$w
            """;
        cmd.Parameters.AddWithValue("$id",document);cmd.Parameters.AddWithValue("$version",version);cmd.Parameters.AddWithValue("$ordinal",segment-1);cmd.Parameters.AddWithValue("$w",workspaceId);
        using var reader=cmd.ExecuteReader();
        if(!reader.Read())throw new InvalidDataException("Citation is missing, deleted or outside the current workspace. No other workspace content was read.");
        return(new(document,reader.GetString(0),version,segment-1,reader.GetString(1),0),reader.GetInt64(2)==1);
    }
}
