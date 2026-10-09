namespace PharmaFlow.Api.Models;

/// <summary>A fictional branch with its canonical location and deterministic weekly schedule.</summary>
public sealed record PharmacyBranch
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public required string FictionalLocation { get; init; }
    public required WeeklyOpeningHours OpeningHours { get; init; }
    public required string TimeZone { get; init; }
}
