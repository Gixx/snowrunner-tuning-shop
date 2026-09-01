using System.Buffers.Binary;
using System.IO.Compression;

namespace SnowRunnerTuningShop.Core.Pak;

/// <summary>
/// Patches a zip entry payload without changing the compressed byte length of its local record.
/// Keeps every later local header at the same file offset (required for SnowRunner initial.pak).
/// </summary>
internal static class PakInPlaceZipPatcher
{
    private const uint LocalFileHeaderSignature = 0x04034B50;
    private const uint CentralDirectoryHeaderSignature = 0x02014B50;

    internal static bool TryReplaceEntry(string pakPath, string entryName, byte[] uncompressedPayload)
    {
        var pakBytes = File.ReadAllBytes(pakPath);
        var entries = PakRawZipReplacer.ReadCentralDirectoryForPatching(pakBytes);
        var normalizedName = entryName.Replace('\\', '/');
        var entry = entries.FirstOrDefault(item => item.Name == normalizedName)
            ?? throw new FileNotFoundException($"Pak entry was not found: {normalizedName}", pakPath);

        var localOffset = (int)entry.LocalHeaderOffset;
        if (BinaryPrimitives.ReadUInt32LittleEndian(pakBytes.AsSpan(localOffset)) != LocalFileHeaderSignature)
        {
            return false;
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(pakBytes.AsSpan(localOffset + 26));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(pakBytes.AsSpan(localOffset + 28));
        var headerLength = 30 + nameLength + extraLength;
        var compressedOffset = localOffset + headerLength;
        var originalCompressedLength = (int)entry.CompressedSize;

        if (!TryCompressToFit(uncompressedPayload, originalCompressedLength, out var compressed, out var crc32))
        {
            return false;
        }

        var originalCompressed = pakBytes.AsSpan(compressedOffset, originalCompressedLength).ToArray();
        var padded = new byte[originalCompressedLength];
        compressed.CopyTo(padded, 0);
        originalCompressed.AsSpan(compressed.Length).CopyTo(padded.AsSpan(compressed.Length));

        if (!VerifyDecompressesTo(padded, uncompressedPayload))
        {
            return false;
        }

        padded.CopyTo(pakBytes, compressedOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(pakBytes.AsSpan(localOffset + 14), crc32);
        BinaryPrimitives.WriteUInt32LittleEndian(pakBytes.AsSpan(localOffset + 22), (uint)uncompressedPayload.Length);

        var centralOffset = FindCentralDirectoryEntryOffset(pakBytes, normalizedName);
        if (centralOffset < 0)
        {
            return false;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(pakBytes.AsSpan(centralOffset + 16), crc32);
        BinaryPrimitives.WriteUInt32LittleEndian(pakBytes.AsSpan(centralOffset + 24), (uint)uncompressedPayload.Length);

        File.WriteAllBytes(pakPath, pakBytes);
        return true;
    }

    private static bool TryCompressToFit(byte[] payload, int maxCompressedLength, out byte[] compressed, out uint crc32)
    {
        compressed = [];
        crc32 = 0;
        byte[]? best = null;
        uint bestCrc = 0;

        foreach (var level in new[] { CompressionLevel.SmallestSize, CompressionLevel.Optimal, CompressionLevel.Fastest })
        {
            if (!TryCompress(payload, level, out var candidate, out var candidateCrc))
            {
                continue;
            }

            if (candidate.Length <= maxCompressedLength && (best is null || candidate.Length < best.Length))
            {
                best = candidate;
                bestCrc = candidateCrc;
            }
        }

        if (best is null)
        {
            return false;
        }

        compressed = best;
        crc32 = bestCrc;
        return true;
    }

    private static bool TryCompress(byte[] payload, CompressionLevel level, out byte[] compressed, out uint crc32)
    {
        compressed = [];
        crc32 = 0;

        try
        {
            using var zipBuffer = new MemoryStream();
            using (var archive = new ZipArchive(zipBuffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("_", level);
                using var entryStream = entry.Open();
                entryStream.Write(payload, 0, payload.Length);
            }

            var zipBytes = zipBuffer.ToArray();
            if (BinaryPrimitives.ReadUInt32LittleEndian(zipBytes.AsSpan(0)) != LocalFileHeaderSignature)
            {
                return false;
            }

            crc32 = BinaryPrimitives.ReadUInt32LittleEndian(zipBytes.AsSpan(14));
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(zipBytes.AsSpan(26));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(zipBytes.AsSpan(28));
            var compressedSize = BinaryPrimitives.ReadInt32LittleEndian(zipBytes.AsSpan(18));
            var headerLength = 30 + nameLength + extraLength;
            compressed = zipBytes.AsSpan(headerLength, compressedSize).ToArray();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyDecompressesTo(byte[] compressed, byte[] expectedUncompressed)
    {
        try
        {
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray().AsSpan().SequenceEqual(expectedUncompressed);
        }
        catch
        {
            return false;
        }
    }

    private static int FindCentralDirectoryEntryOffset(byte[] pakBytes, string entryName)
    {
        var entries = PakRawZipReplacer.ReadCentralDirectoryForPatching(pakBytes);
        var entry = entries.FirstOrDefault(item => item.Name == entryName);
        if (entry is null)
        {
            return -1;
        }

        var eocdOffset = FindEndOfCentralDirectory(pakBytes);
        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(pakBytes.AsSpan(eocdOffset + 10));
        var centralDirectorySize = BinaryPrimitives.ReadUInt32LittleEndian(pakBytes.AsSpan(eocdOffset + 12));
        var centralDirectoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(pakBytes.AsSpan(eocdOffset + 16));

        var offset = (int)centralDirectoryOffset;
        var end = centralDirectoryOffset + centralDirectorySize;
        while (offset < end)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(pakBytes.AsSpan(offset)) != CentralDirectoryHeaderSignature)
            {
                return -1;
            }

            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(pakBytes.AsSpan(offset + 28));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(pakBytes.AsSpan(offset + 30));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(pakBytes.AsSpan(offset + 32));
            var name = System.Text.Encoding.UTF8.GetString(pakBytes, offset + 46, nameLength);
            if (name.Replace('\\', '/') == entryName)
            {
                return offset;
            }

            offset += 46 + nameLength + extraLength + commentLength;
        }

        return -1;
    }

    private static int FindEndOfCentralDirectory(byte[] source)
    {
        for (var i = source.Length - 22; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(i)) == 0x06054B50)
            {
                return i;
            }
        }

        throw new InvalidDataException("End of central directory record was not found.");
    }
}
