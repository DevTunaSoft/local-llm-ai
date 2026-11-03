using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkIntelligence.Services;

/// <summary>Device/user-bound approval markers. Deliberately excluded from portable snapshots.</summary>
internal static class FolderAuthorization
{
    internal const string FileName="folder-approvals.json";
    private static readonly object Gate=new();
    private static readonly string Host=HostIdentity();
    private static string HostIdentity()
    {
        var user=Environment.UserName;
        if(OperatingSystem.IsWindows())
        {using var identity=System.Security.Principal.WindowsIdentity.GetCurrent();user=identity.User?.Value ?? user;}
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName+"\n"+user)));
    }
    private static string Key(string folder,long workspace)
    {
        var full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));if(OperatingSystem.IsWindows())full=full.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workspace.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+full)));
    }
    private static Dictionary<string,string> Read()
    {
        var file=Path.Combine(LocalPaths.Data,FileName);PathSafety.RejectReparsePoints(file);
        if(!File.Exists(file))return new(StringComparer.Ordinal);
        if(new FileInfo(file).Length>262144)throw new InvalidDataException("Folder approval metadata is oversized.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(file)) ?? throw new InvalidDataException("Folder approvals are malformed.");
    }
    internal static bool IsApproved(string folder,long workspace)
    {lock(Gate){var approvals=Read();return approvals.TryGetValue(Key(folder,workspace),out var host)&&host==Host;}}
    internal static void Approve(string folder,long workspace)
    {
        LocalPaths.Ensure();PathSafety.RejectReparsePoints(folder);
        lock(Gate)
        {
            using var fileLock=FileOperationLock.Acquire("folder-approvals");
            var approvals=Read();approvals[Key(folder,workspace)]=Host;
            var json=JsonSerializer.Serialize(approvals);if(json.Length>262144)throw new InvalidDataException("Too many folder approval records.");
            var file=Path.Combine(LocalPaths.Data,FileName);var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,json);File.Move(temp,file,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
