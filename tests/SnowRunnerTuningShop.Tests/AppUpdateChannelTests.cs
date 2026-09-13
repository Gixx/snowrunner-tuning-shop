using SnowRunnerTuningShop.Core.Updates;

namespace SnowRunnerTuningShop.Tests;

public sealed class AppSemVersionTests
{
    [Theory]
    [InlineData("1.3.5", 1, 3, 5, null)]
    [InlineData("v1.3.5", 1, 3, 5, null)]
    [InlineData("1.5.0-beta.1", 1, 5, 0, "beta.1")]
    [InlineData("v1.5.0-beta.2", 1, 5, 0, "beta.2")]
    public void TryParse_AcceptsStableAndBetaTags(
        string text,
        int major,
        int minor,
        int patch,
        string? pre)
    {
        Assert.True(AppSemVersion.TryParse(text, out var version));
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(pre, version.PreRelease);
    }

    [Theory]
    [InlineData("1.5.0-beta.2", "1.5.0-beta.1", 1)]
    [InlineData("1.5.0", "1.5.0-beta.3", 1)]
    [InlineData("1.5.0-beta.1", "1.4.9", 1)]
    [InlineData("1.3.5", "1.3.5", 0)]
    [InlineData("1.3.4", "1.3.5", -1)]
    public void Compare_FollowsSemVerPrecedence(string left, string right, int expectedSign)
    {
        Assert.True(AppSemVersion.TryParse(left, out var a));
        Assert.True(AppSemVersion.TryParse(right, out var b));
        var cmp = a.CompareTo(b);
        Assert.Equal(expectedSign, Math.Sign(cmp));
    }

    [Fact]
    public void ToString_RoundTrips()
    {
        Assert.True(AppSemVersion.TryParse("v1.5.0-beta.3", out var version));
        Assert.Equal("1.5.0-beta.3", version.ToString());
    }
}

public sealed class AppUpdateChannelSelectionTests
{
    [Fact]
    public void Stable_IgnoresPrereleases_EvenIfNewer()
    {
        var tag = AppUpdateService.SelectNewestTagForTests(
            [
                ("v1.3.4", Prerelease: false),
                ("v1.5.0-beta.3", Prerelease: true),
                ("v1.4.0-beta.1", Prerelease: true),
            ],
            AppUpdateChannel.Stable);

        Assert.Equal("1.3.4", tag);
    }

    [Fact]
    public void Beta_PicksHighestAmongStableAndBeta()
    {
        var tag = AppUpdateService.SelectNewestTagForTests(
            [
                ("v1.3.4", Prerelease: false),
                ("v1.5.0-beta.1", Prerelease: true),
                ("v1.5.0-beta.2", Prerelease: true),
            ],
            AppUpdateChannel.Beta);

        Assert.Equal("1.5.0-beta.2", tag);
    }

    [Fact]
    public void Beta_PrefersStableReleaseOverOlderBeta()
    {
        var tag = AppUpdateService.SelectNewestTagForTests(
            [
                ("v1.5.0-beta.3", Prerelease: true),
                ("v1.5.0", Prerelease: false),
            ],
            AppUpdateChannel.Beta);

        Assert.Equal("1.5.0", tag);
    }

    [Fact]
    public void IsSameVersion_UnderstandsBetaTags()
    {
        Assert.True(AppUpdateService.IsSameVersion("v1.5.0-beta.1", "1.5.0-beta.1"));
        Assert.False(AppUpdateService.IsSameVersion("1.5.0-beta.1", "1.5.0-beta.2"));
    }
}
