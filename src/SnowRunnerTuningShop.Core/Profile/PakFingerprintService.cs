using System.Security.Cryptography;

namespace SnowRunnerTuningShop.Core.Profile;

public static class PakFingerprintService
{
    public static PakFingerprintSnapshot ComputeFileFingerprint(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File was not found.", filePath);
        }

        var info = new FileInfo(filePath);
        return new PakFingerprintSnapshot
        {
            Sha256 = ComputeSha256(filePath),
            SizeBytes = info.Length,
            LastWriteTimeUtc = info.LastWriteTimeUtc,
        };
    }

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool FingerprintsMatch(PakFingerprintSnapshot? left, PakFingerprintSnapshot? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return left.SizeBytes == right.SizeBytes
            && string.Equals(left.Sha256, right.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
