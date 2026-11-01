using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace WorkIntelligence.Services;

public static class RuntimeBundle
{
    public static string Resolve(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName) || exeName is "." or ".." ||
            exeName.IndexOfAny(['/', '\\', ':']) >= 0)
            throw new ArgumentException("Choose a single bundled component filename.", nameof(exeName));
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();
        if (!names.Contains("WorkIntelligence.runtime.zip") || !names.Contains("WorkIntelligence.runtime.sha256"))
            throw new FileNotFoundException("This development build does not contain the native AI/backup tools. Release packaging must embed the vetted runtime bundle.");
        using var manifestStream = assembly.GetManifestResourceStream("WorkIntelligence.runtime.sha256")!;
        using var manifestReader = new StreamReader(manifestStream);
        var validHash = manifestReader.ReadToEnd().Trim();
        using var packed = assembly.GetManifestResourceStream("WorkIntelligence.runtime.zip")!;
        if (validHash.Length != 64 || !validHash.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid embedded runtime manifest hash.");
        using var archiveBytes = new MemoryStream();
        packed.CopyTo(archiveBytes);
        if (!Convert.ToHexString(SHA256.HashData(archiveBytes)).Equals(validHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Embedded runtime hash mismatch.");
        archiveBytes.Position = 0;
        var destination = Path.Combine(LocalPaths.Runtime, validHash);
        var output = Path.Combine(destination, exeName);
        ExtractArchive(archiveBytes, destination);
        if (!File.Exists(output)) throw new FileNotFoundException("Required runtime component not present in signed runtime bundle: " + exeName);
        return output;
    }
    // The caller verifies the embedded archive hash before extraction.
    internal static void ExtractArchive(Stream archiveBytes, string destination)
    {
        destination = Path.GetFullPath(destination);
        PathSafety.RejectReparsePoints(destination);
        Directory.CreateDirectory(destination);
        using (var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Read, leaveOpen: true))
        {
            if (archive.Entries.Count > 150) throw new InvalidDataException("Runtime archive contains too many entries.");
            if (archive.Entries.Sum(e => e.Length) > 1024L * 1024 * 1024)
                throw new InvalidDataException("Runtime archive exceeds maximum uncompressed size.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                var relative = entry.FullName.Replace('\\', '/');
                if (relative.StartsWith('/') || relative.Contains(':') ||
                    relative.Split('/').Any(segment => segment is ".." or "."))
                    throw new InvalidDataException("Unsafe runtime archive entry path.");
                if (string.IsNullOrEmpty(entry.Name)) continue;
                if (!paths.Add(relative)) throw new InvalidDataException("Duplicate runtime archive entry path.");
                if (entry.Length > 512L * 1024 * 1024)
                    throw new InvalidDataException("Individual runtime entry exceeds safe limit.");
                // Reject ZIP Unix symlink entries, which may escape the approved extraction root.
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    throw new InvalidDataException("Runtime archive contains an unsupported symbolic link.");
                var target = Path.GetFullPath(Path.Combine(destination, relative));
                if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unsafe runtime archive entry path.");
                PathSafety.RejectReparsePoints(target);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                byte[] expectedHash;
                using (var src = entry.Open())
                using (var hasher = SHA256.Create())
                    expectedHash = hasher.ComputeHash(src);
                if (File.Exists(target))
                {
                    using var existing = File.OpenRead(target);
                    if (SHA256.HashData(existing).AsSpan().SequenceEqual(expectedHash)) continue;
                }
                var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var source = entry.Open())
                    using (var destinationStream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        source.CopyTo(destinationStream);
                        destinationStream.Flush(true);
                    }
                    using (var verified = File.OpenRead(temp))
                        if (!SHA256.HashData(verified).AsSpan().SequenceEqual(expectedHash))
                            throw new InvalidDataException("Runtime extraction verification failed.");
                    File.Move(temp, target, overwrite: true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
    }

}
