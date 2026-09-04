using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SnowRunnerTuningShop.Core.Pak;

/// <summary>
/// Replaces zip entries by copying untouched local file records verbatim from the source pak.
/// Rebuilding/recompressing the whole archive breaks SnowRunner's pak loader.
/// </summary>
internal static class PakRawZipReplacer
{
    private const uint LocalFileHeaderSignature = 0x04034B50;
    private const uint CentralDirectoryHeaderSignature = 0x02014B50;
    private const uint EndOfCentralDirectorySignature = 0x06054B50;
    private const ushort DeflateCompressionMethod = 8;
    private const ushort StoreCompressionMethod = 0;

    internal static void ReplaceEntries(
        string pakPath,
        IReadOnlyDictionary<string, byte[]> replacements,
        IProgress<PakWriteProgress>? progress = null) =>
        ReplaceEntries(pakPath, replacements, useStoreCompression: false, progress);

    /// <summary>
    /// Rebuilds the pak without the given entries, copying every kept local file record verbatim.
    /// </summary>
    internal static int RemoveEntries(string pakPath, IReadOnlyCollection<string> entryNames)
    {
        if (entryNames.Count == 0)
        {
            return 0;
        }

        var sourceBytes = File.ReadAllBytes(pakPath);
        var removeKeys = new HashSet<string>(
            entryNames.Select(key => key.Replace('\\', '/')),
            StringComparer.OrdinalIgnoreCase);
        var allEntries = ReadCentralDirectory(sourceBytes);
        var kept = allEntries
            .Where(entry => !removeKeys.Contains(entry.Name))
            .OrderBy(entry => entry.LocalHeaderOffset)
            .ToArray();
        var removedCount = allEntries.Count - kept.Length;
        if (removedCount == 0)
        {
            return 0;
        }

        WritePak(
            pakPath,
            kept,
            sourceBytes,
            entry => (ReadLocalFileRecordBytes(sourceBytes, entry), entry));
        return removedCount;
    }

    /// <summary>
    /// Appends new entries (or replaces same-named ones) while copying untouched local records verbatim.
    /// </summary>
    internal static void AddEntries(string pakPath, IReadOnlyDictionary<string, byte[]> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var sourceBytes = File.ReadAllBytes(pakPath);
        var addByName = entries.ToDictionary(
            pair => pair.Key.Replace('\\', '/'),
            pair => pair.Value,
            StringComparer.Ordinal);
        var replaceKeys = new HashSet<string>(addByName.Keys, StringComparer.OrdinalIgnoreCase);
        var kept = ReadCentralDirectory(sourceBytes)
            .Where(entry => !replaceKeys.Contains(entry.Name))
            .OrderBy(entry => entry.LocalHeaderOffset)
            .ToArray();

        var append = new List<(byte[] Record, CentralDirectoryEntry Metadata)>(addByName.Count);
        foreach (var (name, payload) in addByName)
        {
            var prepared = PrepareDeflatePayload(payload);
            using var record = new MemoryStream();
            WriteLocalFileHeader(
                record,
                name,
                flags: 0,
                lastModTime: 0,
                lastModDate: 0,
                extraField: ReadOnlySpan<byte>.Empty,
                prepared.Payload,
                prepared.Crc32,
                payload.Length,
                prepared.CompressionMethod);
            record.Write(prepared.Payload, 0, prepared.Payload.Length);
            append.Add((
                record.ToArray(),
                new CentralDirectoryEntry
                {
                    Name = name,
                    Flags = 0,
                    CompressionMethod = prepared.CompressionMethod,
                    LastModTime = 0,
                    LastModDate = 0,
                    Crc32 = prepared.Crc32,
                    CompressedSize = (uint)prepared.Payload.Length,
                    UncompressedSize = (uint)payload.Length,
                    ExternalAttributes = 0,
                    LocalHeaderOffset = 0,
                    ExtraField = [],
                }));
        }

        WritePak(
            pakPath,
            kept,
            sourceBytes,
            entry => (ReadLocalFileRecordBytes(sourceBytes, entry), entry),
            appendEntries: append);
    }

    internal static void CopyEntriesFromSource(
        string targetPakPath,
        string sourcePakPath,
        IReadOnlyCollection<string> entryNames)
    {
        var sourceBytes = File.ReadAllBytes(sourcePakPath);
        var targetBytes = File.ReadAllBytes(targetPakPath);
        var sourceEntries = ReadCentralDirectory(sourceBytes)
            .ToDictionary(entry => entry.Name, StringComparer.Ordinal);
        var targetEntries = ReadCentralDirectory(targetBytes)
            .OrderBy(entry => entry.LocalHeaderOffset)
            .ToArray();
        var copyKeys = new HashSet<string>(
            entryNames.Select(key => key.Replace('\\', '/')),
            StringComparer.Ordinal);

        foreach (var key in copyKeys)
        {
            if (!sourceEntries.ContainsKey(key))
            {
                throw new FileNotFoundException($"Pak entry was not found in source: {key}", sourcePakPath);
            }

            if (targetEntries.All(entry => !string.Equals(entry.Name, key, StringComparison.Ordinal)))
            {
                throw new FileNotFoundException($"Pak entry was not found in target: {key}", targetPakPath);
            }
        }

        WritePak(
            targetPakPath,
            targetEntries,
            targetBytes,
            entry =>
            {
                if (!copyKeys.Contains(entry.Name))
                {
                    return (ReadLocalFileRecordBytes(targetBytes, entry), entry);
                }

                var sourceEntry = sourceEntries[entry.Name];
                return (ReadLocalFileRecordBytes(sourceBytes, sourceEntry), sourceEntry);
            });
    }

    private static void ReplaceEntries(
        string pakPath,
        IReadOnlyDictionary<string, byte[]> replacements,
        bool useStoreCompression,
        IProgress<PakWriteProgress>? progress = null)
    {
        var sourceBytes = File.ReadAllBytes(pakPath);
        var entries = ReadCentralDirectory(sourceBytes);
        var replacementKeys = new HashSet<string>(
            replacements.Keys.Select(key => key.Replace('\\', '/')),
            StringComparer.Ordinal);

        foreach (var key in replacementKeys)
        {
            if (entries.All(entry => !string.Equals(entry.Name, key, StringComparison.Ordinal)))
            {
                throw new FileNotFoundException($"Pak entry was not found: {key}", pakPath);
            }
        }

        var ordered = entries.OrderBy(entry => entry.LocalHeaderOffset).ToArray();
        WritePak(
            pakPath,
            ordered,
            sourceBytes,
            entry =>
            {
                if (!replacementKeys.Contains(entry.Name))
                {
                    return (ReadLocalFileRecordBytes(sourceBytes, entry), entry);
                }

                var payload = replacements[entry.Name];
                var prepared = useStoreCompression
                    ? PrepareStoredPayload(payload)
                    : PrepareDeflatePayload(payload);

                using var record = new MemoryStream();
                WriteLocalFileHeader(
                    record,
                    entry.Name,
                    entry.Flags,
                    entry.LastModTime,
                    entry.LastModDate,
                    entry.ExtraField,
                    prepared.Payload,
                    prepared.Crc32,
                    payload.Length,
                    prepared.CompressionMethod);
                record.Write(prepared.Payload, 0, prepared.Payload.Length);

                var updatedEntry = entry with
                {
                    CompressionMethod = prepared.CompressionMethod,
                    Crc32 = prepared.Crc32,
                    CompressedSize = (uint)prepared.Payload.Length,
                    UncompressedSize = (uint)payload.Length,
                };

                return (record.ToArray(), updatedEntry);
            },
            progress);
    }

    private static void WritePak(
        string pakPath,
        IReadOnlyList<CentralDirectoryEntry> orderedEntries,
        byte[] sourceBytes,
        Func<CentralDirectoryEntry, (byte[] Record, CentralDirectoryEntry Metadata)> buildEntry,
        IProgress<PakWriteProgress>? progress = null,
        IReadOnlyList<(byte[] Record, CentralDirectoryEntry Metadata)>? appendEntries = null)
    {
        var appendCount = appendEntries?.Count ?? 0;
        var output = new MemoryStream(sourceBytes.Length);
        var updatedCentral = new List<CentralDirectoryEntry>(orderedEntries.Count + appendCount);
        var total = orderedEntries.Count + appendCount;
        var lastReportedStep = 0;
        var lastReportUtc = DateTime.MinValue;

        progress?.Report(new PakWriteProgress(PakWritePhase.Writing, 0, Math.Max(total, 1)));

        for (var index = 0; index < orderedEntries.Count; index++)
        {
            var entry = orderedEntries[index];
            var (record, metadata) = buildEntry(entry);
            var localOffset = (uint)output.Length;
            output.Write(record, 0, record.Length);
            updatedCentral.Add(metadata with { LocalHeaderOffset = localOffset });

            var current = index + 1;
            var now = DateTime.UtcNow;
            var isComplete = current >= total;
            var isStart = current <= 1;
            if (progress is not null
                && (isComplete
                    || isStart
                    || current - lastReportedStep >= 25
                    || (now - lastReportUtc).TotalMilliseconds >= 1000))
            {
                lastReportedStep = current;
                lastReportUtc = now;
                progress.Report(new PakWriteProgress(PakWritePhase.Writing, current, total, entry.Name));
            }
        }

        if (appendEntries is not null)
        {
            for (var index = 0; index < appendEntries.Count; index++)
            {
                var (record, metadata) = appendEntries[index];
                var localOffset = (uint)output.Length;
                output.Write(record, 0, record.Length);
                updatedCentral.Add(metadata with { LocalHeaderOffset = localOffset });

                var current = orderedEntries.Count + index + 1;
                progress?.Report(new PakWriteProgress(PakWritePhase.Writing, current, total, metadata.Name));
            }
        }

        var centralOffset = (uint)output.Length;
        foreach (var entry in updatedCentral)
        {
            WriteCentralDirectoryHeader(output, entry);
        }

        WriteEndOfCentralDirectory(
            output,
            (uint)updatedCentral.Count,
            (uint)(output.Length - centralOffset),
            centralOffset);

        progress?.Report(new PakWriteProgress(PakWritePhase.Saving, 0, 1, EntryName: pakPath));
        File.WriteAllBytes(pakPath, output.ToArray());
        progress?.Report(new PakWriteProgress(PakWritePhase.Saving, 1, 1, EntryName: pakPath));
    }

    private static byte[] ReadLocalFileRecordBytes(byte[] source, CentralDirectoryEntry entry)
    {
        var span = ReadLocalFileRecord(source, entry);
        return span.ToArray();
    }

    private static ReadOnlySpan<byte> ReadLocalFileRecord(byte[] source, CentralDirectoryEntry entry)
    {
        var offset = (int)entry.LocalHeaderOffset;
        if (BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset)) != LocalFileHeaderSignature)
        {
            throw new InvalidDataException($"Invalid local header for {entry.Name}.");
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 26));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 28));
        var headerLength = 30 + nameLength + extraLength;
        var dataDescriptorLength = (entry.Flags & 0x08) != 0 ? 16 : 0;
        var totalLength = headerLength + (int)entry.CompressedSize + dataDescriptorLength;
        return source.AsSpan(offset, totalLength);
    }

    private static PreparedPayload PrepareStoredPayload(byte[] payload)
    {
        using var zipBuffer = new MemoryStream();
        using (var archive = new ZipArchive(zipBuffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("_", CompressionLevel.NoCompression);
            using var entryStream = entry.Open();
            entryStream.Write(payload, 0, payload.Length);
        }

        var zipBytes = zipBuffer.ToArray();
        if (BinaryPrimitives.ReadUInt32LittleEndian(zipBytes.AsSpan(0)) != LocalFileHeaderSignature)
        {
            throw new InvalidDataException("Could not store pak entry payload.");
        }

        var method = BinaryPrimitives.ReadUInt16LittleEndian(zipBytes.AsSpan(8));
        if (method != StoreCompressionMethod)
        {
            throw new InvalidDataException("Zip store entry was not created.");
        }

        var crc = BinaryPrimitives.ReadUInt32LittleEndian(zipBytes.AsSpan(14));
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(zipBytes.AsSpan(26));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(zipBytes.AsSpan(28));
        var storedSize = BinaryPrimitives.ReadInt32LittleEndian(zipBytes.AsSpan(18));
        var headerLength = 30 + nameLength + extraLength;
        var stored = zipBytes.AsSpan(headerLength, storedSize).ToArray();
        return new PreparedPayload(StoreCompressionMethod, stored, crc);
    }

    private static PreparedPayload PrepareDeflatePayload(byte[] payload)
    {
        using var zipBuffer = new MemoryStream();
        using (var archive = new ZipArchive(zipBuffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("_", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(payload, 0, payload.Length);
        }

        var zipBytes = zipBuffer.ToArray();
        if (BinaryPrimitives.ReadUInt32LittleEndian(zipBytes.AsSpan(0)) != LocalFileHeaderSignature)
        {
            throw new InvalidDataException("Could not compress pak entry payload.");
        }

        var crc = BinaryPrimitives.ReadUInt32LittleEndian(zipBytes.AsSpan(14));
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(zipBytes.AsSpan(26));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(zipBytes.AsSpan(28));
        var compressedSize = BinaryPrimitives.ReadInt32LittleEndian(zipBytes.AsSpan(18));
        var headerLength = 30 + nameLength + extraLength;
        var compressed = zipBytes.AsSpan(headerLength, compressedSize).ToArray();
        return new PreparedPayload(DeflateCompressionMethod, compressed, crc);
    }

    private static void WriteLocalFileHeader(
        Stream output,
        string entryName,
        ushort flags,
        ushort lastModTime,
        ushort lastModDate,
        ReadOnlySpan<byte> extraField,
        byte[] payload,
        uint crc32,
        int uncompressedSize,
        ushort compressionMethod)
    {
        var nameBytes = Encoding.UTF8.GetBytes(entryName);
        Span<byte> header = stackalloc byte[30];
        BinaryPrimitives.WriteUInt32LittleEndian(header[..4], LocalFileHeaderSignature);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..6], 20);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..8], (ushort)(flags & ~0x08));
        BinaryPrimitives.WriteUInt16LittleEndian(header[8..10], compressionMethod);
        BinaryPrimitives.WriteUInt16LittleEndian(header[10..12], lastModTime);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..14], lastModDate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[14..18], crc32);
        BinaryPrimitives.WriteUInt32LittleEndian(header[18..22], (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header[22..26], (uint)uncompressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header[26..28], (ushort)nameBytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[28..30], (ushort)extraField.Length);
        output.Write(header);
        output.Write(nameBytes);
        if (!extraField.IsEmpty)
        {
            output.Write(extraField);
        }
    }

    private static void WriteCentralDirectoryHeader(Stream output, CentralDirectoryEntry entry)
    {
        var nameBytes = Encoding.UTF8.GetBytes(entry.Name);
        Span<byte> header = stackalloc byte[46];
        BinaryPrimitives.WriteUInt32LittleEndian(header[..4], CentralDirectoryHeaderSignature);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..6], 20);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..8], 20);
        BinaryPrimitives.WriteUInt16LittleEndian(header[8..10], (ushort)(entry.Flags & ~0x08));
        BinaryPrimitives.WriteUInt16LittleEndian(header[10..12], entry.CompressionMethod);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..14], entry.LastModTime);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..16], entry.LastModDate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..20], entry.Crc32);
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..24], entry.CompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..28], entry.UncompressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header[28..30], (ushort)nameBytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[30..32], (ushort)entry.ExtraField.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..34], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..36], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header[36..38], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header[38..42], entry.ExternalAttributes);
        BinaryPrimitives.WriteUInt32LittleEndian(header[42..46], entry.LocalHeaderOffset);
        output.Write(header);
        output.Write(nameBytes);
        if (entry.ExtraField.Length > 0)
        {
            output.Write(entry.ExtraField);
        }
    }

    private static void WriteEndOfCentralDirectory(
        Stream output,
        uint entryCount,
        uint centralDirectorySize,
        uint centralDirectoryOffset)
    {
        Span<byte> eocd = stackalloc byte[22];
        BinaryPrimitives.WriteUInt32LittleEndian(eocd[..4], EndOfCentralDirectorySignature);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[4..6], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[6..8], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[8..10], (ushort)entryCount);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[10..12], (ushort)entryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(eocd[12..16], centralDirectorySize);
        BinaryPrimitives.WriteUInt32LittleEndian(eocd[16..20], centralDirectoryOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[20..22], 0);
        output.Write(eocd);
    }

    internal static IReadOnlyList<PakZipEntryInfo> ReadCentralDirectoryForPatching(byte[] source)
    {
        var entries = ReadCentralDirectory(source);
        return entries.Select(entry => new PakZipEntryInfo
        {
            Name = entry.Name,
            LocalHeaderOffset = entry.LocalHeaderOffset,
            CompressedSize = entry.CompressedSize,
            UncompressedSize = entry.UncompressedSize,
        }).ToArray();
    }

    private static List<CentralDirectoryEntry> ReadCentralDirectory(byte[] source)
    {
        var eocdOffset = FindEndOfCentralDirectory(source);
        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(eocdOffset + 10));
        var centralDirectorySize = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(eocdOffset + 12));
        var centralDirectoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(eocdOffset + 16));

        var entries = new List<CentralDirectoryEntry>(entryCount);
        var offset = (int)centralDirectoryOffset;
        var end = centralDirectoryOffset + centralDirectorySize;
        while (offset < end)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset)) != CentralDirectoryHeaderSignature)
            {
                throw new InvalidDataException("Invalid central directory header.");
            }

            var flags = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 8));
            var compressionMethod = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 10));
            var lastModTime = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 12));
            var lastModDate = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 14));
            var crc32 = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset + 16));
            var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset + 20));
            var uncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset + 24));
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 28));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 30));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(offset + 32));
            var externalAttributes = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset + 38));
            var localHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset + 42));
            var nameStart = offset + 46;
            var name = Encoding.UTF8.GetString(source, nameStart, nameLength);
            var extraStart = nameStart + nameLength;
            var extra = source.AsSpan(extraStart, extraLength).ToArray();

            entries.Add(new CentralDirectoryEntry
            {
                Name = name.Replace('\\', '/'),
                Flags = flags,
                CompressionMethod = compressionMethod,
                LastModTime = lastModTime,
                LastModDate = lastModDate,
                Crc32 = crc32,
                CompressedSize = compressedSize,
                UncompressedSize = uncompressedSize,
                ExternalAttributes = externalAttributes,
                LocalHeaderOffset = localHeaderOffset,
                ExtraField = extra,
            });

            offset = extraStart + extraLength + commentLength;
        }

        return entries;
    }

    private static int FindEndOfCentralDirectory(byte[] source)
    {
        for (var i = source.Length - 22; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(i)) == EndOfCentralDirectorySignature)
            {
                return i;
            }
        }

        throw new InvalidDataException("End of central directory record was not found.");
    }

    private sealed record PreparedPayload(ushort CompressionMethod, byte[] Payload, uint Crc32);

    private sealed record CentralDirectoryEntry
    {
        public required string Name { get; init; }
        public ushort Flags { get; init; }
        public ushort CompressionMethod { get; init; }
        public ushort LastModTime { get; init; }
        public ushort LastModDate { get; init; }
        public uint Crc32 { get; init; }
        public uint CompressedSize { get; init; }
        public uint UncompressedSize { get; init; }
        public uint ExternalAttributes { get; init; }
        public uint LocalHeaderOffset { get; init; }
        public byte[] ExtraField { get; init; } = [];
    }
}

internal sealed record PakZipEntryInfo
{
    public required string Name { get; init; }
    public uint LocalHeaderOffset { get; init; }
    public uint CompressedSize { get; init; }
    public uint UncompressedSize { get; init; }
}
