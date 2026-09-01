namespace SnowRunnerTuningShop.Core.PhotoMode;

internal static class PhotoModeSslBundleEditor
{
    internal const string ReleaseBundle = "[ssl_cache]/initial_release.sslbundle";
    internal const string DebugBundle = "[ssl_cache]/initial_debug.sslbundle";
    internal const string ProfileBundle = "[ssl_cache]/initial_profile.sslbundle";

    internal static readonly string[] BundlePaths =
    [
        ReleaseBundle,
        DebugBundle,
        ProfileBundle,
    ];

    private static readonly byte[] PhotoModeTimePrefix =
    [
        0x1A, 0x0E, 0x08, 0x1A, 0x10, 0x16, 0x18, 0x02, 0x20,
    ];

    private static readonly byte[] PhotoModeTimeSuffix =
    [
        0x3A, 0x04, 0x0A, 0x02, 0x08, 0x08,
        0x1A, 0x0E, 0x08, 0x1B, 0x10, 0x17, 0x18, 0x02,
    ];

    private static readonly byte[] InitTimePrefix =
    [
        0x42, 0x04, 0x08, 0x30, 0x10, 0x01, 0x1A, 0x20, 0x08, 0xA2, 0x01, 0x10, 0x3C, 0x18, 0x05,
        0x3A, 0x11, 0x0A, 0x03, 0x08, 0x84, 0x02, 0x0A, 0x0A, 0x08, 0x85, 0x02, 0x12, 0x05, 0x08,
        0x04, 0x28, 0x86, 0x02, 0x42, 0x04, 0x08, 0x31, 0x10,
    ];

    internal static int ReadInitTimeIndex(byte[] bundleBytes)
    {
        var initOffset = FindSingle(bundleBytes, InitTimePrefix, "time init marker");
        return bundleBytes[initOffset + InitTimePrefix.Length];
    }

    internal static int ReadTimeIndex(byte[] bundleBytes)
    {
        var timeOffset = FindPhotoModeTimeByteOffset(bundleBytes);
        return bundleBytes[timeOffset];
    }

    internal static byte[] WriteTimeIndex(byte[] bundleBytes, int timeIndex)
    {
        if (timeIndex is < PhotoModeTimeIndex.GameDefault or > PhotoModeTimeIndex.PresetMaximum)
        {
            throw new PhotoModeLoadException($"Time index {timeIndex} is out of range.");
        }

        var updated = (byte[])bundleBytes.Clone();
        var timeOffset = FindPhotoModeTimeByteOffset(updated);
        if (updated[timeOffset] == (byte)timeIndex)
        {
            return bundleBytes;
        }

        // Only patch the photo-mode preset slot byte. The separate init-time marker
        // must stay untouched — changing it crashes SnowRunner during boot.
        updated[timeOffset] = (byte)timeIndex;
        return updated;
    }

    private static int FindPhotoModeTimeByteOffset(byte[] bundleBytes)
    {
        var matches = 0;
        var timeOffset = -1;
        var offset = 0;
        while (offset <= bundleBytes.Length - PhotoModeTimePrefix.Length)
        {
            var prefixIndex = bundleBytes.AsSpan(offset).IndexOf(PhotoModeTimePrefix);
            if (prefixIndex < 0)
            {
                break;
            }

            prefixIndex += offset;
            var candidate = prefixIndex + PhotoModeTimePrefix.Length;
            if (candidate < bundleBytes.Length &&
                bundleBytes.AsSpan(candidate + 1).StartsWith(PhotoModeTimeSuffix))
            {
                matches++;
                timeOffset = candidate;
            }

            offset = prefixIndex + 1;
        }

        if (matches != 1)
        {
            throw new PhotoModeLoadException(
                $"Photo mode time preset was not found in sslbundle (matches: {matches}). The game may have updated.");
        }

        return timeOffset;
    }

    private static int FindSingle(byte[] haystack, byte[] needle, string label)
    {
        var count = 0;
        var index = -1;
        var offset = 0;
        while (offset <= haystack.Length - needle.Length)
        {
            var found = haystack.AsSpan(offset).IndexOf(needle);
            if (found < 0)
            {
                break;
            }

            count++;
            index = offset + found;
            offset = index + 1;
        }

        if (count != 1)
        {
            throw new PhotoModeLoadException(
                $"Photo mode {label} was not found in sslbundle (matches: {count}). The game may have updated.");
        }

        return index;
    }
}
