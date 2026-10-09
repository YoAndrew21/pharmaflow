namespace PharmaFlow.Api.Models;

/// <summary>Seven canonical weekdays; an empty list means closed and multiple intervals support split hours.</summary>
public sealed record WeeklyOpeningHours
{
    public required IReadOnlyList<OpeningInterval> Monday { get; init; }
    public required IReadOnlyList<OpeningInterval> Tuesday { get; init; }
    public required IReadOnlyList<OpeningInterval> Wednesday { get; init; }
    public required IReadOnlyList<OpeningInterval> Thursday { get; init; }
    public required IReadOnlyList<OpeningInterval> Friday { get; init; }
    public required IReadOnlyList<OpeningInterval> Saturday { get; init; }
    public required IReadOnlyList<OpeningInterval> Sunday { get; init; }
}
