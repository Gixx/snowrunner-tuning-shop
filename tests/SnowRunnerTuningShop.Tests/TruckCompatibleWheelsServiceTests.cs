using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class TruckCompatibleWheelsServiceTests
{
    private const string BaselineFord =
        """
        <Truck>
          <TruckData>
            <CompatibleWheels Scale="0.47" Type="wheels_ford_f750" />
            <CompatibleWheels Scale="0.52" Type="wheels_ford_f750" />
            <CompatibleWheels Scale="0.57" Type="wheels_ford_f750" />
            <CompatibleWheels Scale="0.47" Type="wheels_medium_offroad_double" />
            <CompatibleWheels Scale="0.52" Type="wheels_medium_offroad_double" />
            <CompatibleWheels Scale="0.57" Type="wheels_medium_offroad_double" />
          </TruckData>
        </Truck>
        """;

    [Fact]
    public void Note_kind_marks_dual_rear_and_front_variant()
    {
        var (frontKind, related) = TruckCompatibleWheelsService.ResolveNoteForTests(
            "wheels_medium_double_front",
            hasWidthRear: false,
            dualRearSetIds: ["wheels_medium_double"]);
        Assert.Equal(TruckWheelSetNoteKind.SingleWidthDualVariant, frontKind);
        Assert.Equal("wheels_medium_double", related);

        var (dualKind, dualRelated) = TruckCompatibleWheelsService.ResolveNoteForTests(
            "wheels_medium_double",
            hasWidthRear: true,
            dualRearSetIds: ["wheels_medium_double"]);
        Assert.Equal(TruckWheelSetNoteKind.DualRear, dualKind);
        Assert.Null(dualRelated);
    }

    [Fact]
    public void Tire_labels_resolve_community_names_like_uod()
    {
        const string xml =
            """
            <TruckWheels>
              <TruckTire Name="offroad_1">
                <GameData>
                  <UiDesc UiName="UI_TIRE_OFFROAD_DOUBLE_1_NAME" />
                </GameData>
              </TruckTire>
              <TruckTire Name="offroad_2">
                <GameData>
                  <UiDesc UiName="UI_TIRE_OFFROAD_DOUBLE_2_NAME" />
                </GameData>
              </TruckTire>
            </TruckWheels>
            """;

        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UI_TIRE_OFFROAD_DOUBLE_1_NAME"] = "UOD I",
            ["UI_TIRE_OFFROAD_DOUBLE_2_NAME"] = "UOD II",
        };

        var labels = TruckCompatibleWheelsService.ExtractTireLabelsForTests(xml, strings);
        Assert.Equal(["UOD I", "UOD II"], labels);
    }

    [Fact]
    public void Extra_candidates_use_clamped_step_and_cap_at_0_99()
    {
        var twoStep = TruckCompatibleWheelsService.ComputeExtraScaleCandidatesForTests([0.47, 0.52, 0.57]);
        Assert.Equal([0.62, 0.67], twoStep);

        var nearCap = TruckCompatibleWheelsService.ComputeExtraScaleCandidatesForTests([0.9, 0.94]);
        Assert.Equal([0.98], nearCap); // +0.04 ok; +0.08 would be 1.02 > 0.99

        var atCap = TruckCompatibleWheelsService.ComputeExtraScaleCandidatesForTests([0.99]);
        Assert.Empty(atCap);

        var single = TruckCompatibleWheelsService.ComputeExtraScaleCandidatesForTests([0.63]);
        Assert.Equal([0.68, 0.73], single);
    }

    [Fact]
    public void Inches_label_uses_scale_times_79()
    {
        Assert.Equal("45\"", TruckCompatibleWheelsService.FormatInchesLabelForTests(0.57));
        Assert.Equal("47\"", TruckCompatibleWheelsService.FormatInchesLabelForTests(0.60));
    }

    [Fact]
    public void Snapshot_locks_vanilla_and_offers_extras()
    {
        var snapshot = TruckCompatibleWheelsService.BuildSizesSnapshotForTests(BaselineFord, BaselineFord);
        Assert.True(snapshot.HasCompatibleWheels);
        Assert.Equal(2, snapshot.AssignedSetCount);
        Assert.Equal(5, snapshot.Sizes.Count); // 3 vanilla + 2 extras
        Assert.All(snapshot.Sizes.Where(s => s.IsVanilla), s => Assert.True(s.IsLocked && s.IsEnabled));
        Assert.All(snapshot.Sizes.Where(s => !s.IsVanilla), s => Assert.False(s.IsLocked));
        Assert.Empty(snapshot.OffsetRows); // extras not enabled yet
        Assert.Equal(2, snapshot.DefaultOffsetByType.Count);
        Assert.True(snapshot.DefaultOffsetByType.ContainsKey("wheels_ford_f750"));
    }

    [Fact]
    public void Apply_sizes_adds_extras_with_default_offset_and_stays_safe()
    {
        var updated = TruckCompatibleWheelsService.ApplySizesToTextForTests(
            BaselineFord,
            BaselineFord,
            enabledExtraScales: [0.62, 0.67],
            offsets:
            [
                new TruckWheelOffsetEdit(0.62, "wheels_ford_f750", 0.02),
                new TruckWheelOffsetEdit(0.67, "wheels_medium_offroad_double", null),
            ]);

        AssertSafe(updated, "ford extras");
        Assert.Contains("""Scale="0.57" Type="wheels_ford_f750" """, updated, StringComparison.Ordinal);
        Assert.Contains("""OffsetZ="0.02" Scale="0.62" Type="wheels_ford_f750" """, updated, StringComparison.Ordinal);
        Assert.Contains("""Scale="0.62" Type="wheels_medium_offroad_double" """, updated, StringComparison.Ordinal);
        Assert.Contains("""Scale="0.67" Type="wheels_ford_f750" """, updated, StringComparison.Ordinal);
        Assert.Contains("""Scale="0.67" Type="wheels_medium_offroad_double" """, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_sizes_removes_deselected_extras()
    {
        var withExtras = TruckCompatibleWheelsService.ApplySizesToTextForTests(
            BaselineFord,
            BaselineFord,
            [0.62],
            []);

        var cleared = TruckCompatibleWheelsService.ApplySizesToTextForTests(
            withExtras,
            BaselineFord,
            [],
            []);

        AssertSafe(cleared, "cleared extras");
        Assert.DoesNotContain("""Scale="0.62" """, cleared, StringComparison.Ordinal);
        Assert.Contains("""Scale="0.57" Type="wheels_ford_f750" """, cleared, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_sets_adds_type_for_every_scale_and_requires_one()
    {
        var updated = TruckCompatibleWheelsService.ApplySetsToTextForTests(
            BaselineFord,
            ["wheels_ford_f750", "wheels_medium_mudtires_double"]);

        AssertSafe(updated, "add set");
        Assert.DoesNotContain("wheels_medium_offroad_double", updated, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("""Type="wheels_medium_mudtires_double" """, updated, StringComparison.Ordinal);
        Assert.Contains("""Scale="0.47" Type="wheels_medium_mudtires_double" """, updated, StringComparison.Ordinal);
        Assert.Contains("""Scale="0.52" Type="wheels_medium_mudtires_double" """, updated, StringComparison.Ordinal);
        Assert.Contains("""Scale="0.57" Type="wheels_medium_mudtires_double" """, updated, StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() =>
            TruckCompatibleWheelsService.ApplySetsToTextForTests(BaselineFord, []));
    }

    [Fact]
    public void Apply_sets_preserves_existing_extra_scales_for_new_type()
    {
        var withExtra = TruckCompatibleWheelsService.ApplySizesToTextForTests(
            BaselineFord,
            BaselineFord,
            [0.62],
            []);

        var updated = TruckCompatibleWheelsService.ApplySetsToTextForTests(
            withExtra,
            ["wheels_ford_f750", "wheels_medium_offroad_double", "wheels_medium_highway_double"]);

        AssertSafe(updated, "set with extra scale");
        Assert.Contains("""Scale="0.62" Type="wheels_medium_highway_double" """, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_reports_suspension_wheel_restriction()
    {
        const string withRestriction =
            """
            <Truck>
              <TruckData>
                <SuspensionSocket MaxWheelRadiusWithoutSuspension="0.105" Type="suspensions_scout" />
                <CompatibleWheels Scale="0.4" Type="wheels_scout1" />
                <CompatibleWheels Scale="0.45" Type="wheels_scout1" />
              </TruckData>
            </Truck>
            """;

        var snapshot = TruckCompatibleWheelsService.BuildSizesSnapshotForTests(withRestriction, withRestriction);
        Assert.True(snapshot.HasSuspensionWheelRestriction);
        Assert.True(snapshot.HasBaselineSuspensionWheelRestriction);
        Assert.Equal(0.105, snapshot.BaselineMaxWheelRadiusWithoutSuspension);
        Assert.False(snapshot.SuspensionRestrictionDisabled);
    }

    [Fact]
    public void Apply_sizes_skips_wheel_rewrite_when_working_has_no_types()
    {
        const string baseline =
            """
            <Truck>
              <TruckData>
                <CompatibleWheels Scale="0.73" Type="wheels_heavy_double_p16" />
                <CompatibleWheels Scale="0.73" Type="wheels_heavy_offroad_double_p16" />
              </TruckData>
            </Truck>
            """;
        const string working =
            """
            <Truck>
              <TruckData>
                <ExtraWheels />
              </TruckData>
            </Truck>
            """;

        var updated = TruckCompatibleWheelsService.ApplySizesToTextForTests(
            working,
            baseline,
            enabledExtraScales: [],
            offsets: []);

        Assert.Equal(working, updated);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TruckCompatibleWheelsService.ApplySizesToTextForTests(
                working,
                baseline,
                enabledExtraScales: [0.78],
                offsets: []));
        Assert.Contains("no CompatibleWheels types", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_sizes_can_disable_and_restore_suspension_restriction()
    {
        const string baseline =
            """
            <Truck>
              <TruckData>
                <SuspensionSocket MaxWheelRadiusWithoutSuspension="0.105" Type="suspensions_scout" />
                <CompatibleWheels Scale="0.4" Type="wheels_scout1" />
                <CompatibleWheels Scale="0.45" Type="wheels_scout1" />
              </TruckData>
            </Truck>
            """;

        var disabled = TruckCompatibleWheelsService.ApplySizesToTextForTests(
            baseline,
            baseline,
            enabledExtraScales: [],
            offsets: [],
            disableSuspensionRestriction: true);

        AssertSafe(disabled, "disable suspension restriction");
        Assert.Contains("""MaxWheelRadiusWithoutSuspension="2.0" """, disabled, StringComparison.Ordinal);

        var disabledSnapshot = TruckCompatibleWheelsService.BuildSizesSnapshotForTests(disabled, baseline);
        Assert.True(disabledSnapshot.SuspensionRestrictionDisabled);

        var restored = TruckCompatibleWheelsService.ApplySizesToTextForTests(
            disabled,
            baseline,
            enabledExtraScales: [],
            offsets: [],
            disableSuspensionRestriction: false);

        AssertSafe(restored, "restore suspension restriction");
        Assert.Contains("""MaxWheelRadiusWithoutSuspension="0.105" """, restored, StringComparison.Ordinal);
        Assert.False(TruckCompatibleWheelsService.BuildSizesSnapshotForTests(restored, baseline).SuspensionRestrictionDisabled);
    }

    private static void AssertSafe(string xml, string context)
    {
        Assert.True(
            XmlRewriteSafety.TryValidate(xml, out var failure),
            $"{context}: {failure}");
    }
}
