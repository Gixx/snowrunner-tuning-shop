using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class PlantWinchSocketXmlTests
{
    private const string SmallTreeWithWinch =
        """
        <_templates Include="environment" />
        <PlantBrand
        	_template="SmallTree"
        	PlantingMinSpacing="0.8"
        >
        	<PhysicsModel>
        		<Body _template="SmallTreeRoot" ModelFrame="BoneRoot" />
        	</PhysicsModel>
        	<GameData>
        		<WinchSocket Pos="(0;0.316; -0.193)" ParentFrame="BoneRoot" />
        	</GameData>
        </PlantBrand>
        """;

    private const string BigTreeWithWinch =
        """
        <PlantBrand _template="BigTree">
        	<GameData>
        		<WinchSocket Pos="(0;1.016; -0.193)" ParentFrame="BoneRoot_cdt" />
        	</GameData>
        </PlantBrand>
        """;

    [Fact]
    public void IsWeakPlant_detects_small_tree_not_big_tree()
    {
        Assert.True(PlantWinchSocketXml.IsWeakPlant(SmallTreeWithWinch));
        Assert.False(PlantWinchSocketXml.IsWeakPlant(BigTreeWithWinch));
    }

    [Fact]
    public void Strip_removes_winch_socket_and_empty_gamedata()
    {
        var stripped = PlantWinchSocketXml.StripWinchSockets(SmallTreeWithWinch);

        Assert.True(XmlRewriteSafety.TryValidate(stripped, out var failure), failure);
        Assert.False(PlantWinchSocketXml.HasWinchSocket(stripped));
        Assert.DoesNotContain("<GameData", stripped, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_template=\"SmallTree\"", stripped, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_puts_winch_socket_back()
    {
        var stripped = PlantWinchSocketXml.StripWinchSockets(SmallTreeWithWinch);
        var restored = PlantWinchSocketXml.RestoreWinchSockets(stripped, SmallTreeWithWinch);

        Assert.True(XmlRewriteSafety.TryValidate(restored, out var failure), failure);
        Assert.True(PlantWinchSocketXml.HasWinchSocket(restored));
        Assert.Contains("WinchSocket Pos=\"(0;0.316; -0.193)\"", restored, StringComparison.Ordinal);
    }
}
