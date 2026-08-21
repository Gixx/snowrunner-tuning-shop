namespace SnowRunnerTuningShop.Core.Models;

public sealed class WinchDefinition
{
    public required string EntryPath { get; init; }
    public required string Name { get; init; }
    public string UiNameKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public required string SourceFile { get; init; }
    public required string Category { get; init; }
    public int Price { get; init; }
    public double Length { get; set; }
    public double StrengthMult { get; set; }
    public bool IsEngineIgnitionRequired { get; set; }
}
