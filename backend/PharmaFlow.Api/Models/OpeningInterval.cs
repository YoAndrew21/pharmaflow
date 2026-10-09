namespace PharmaFlow.Api.Models;

/// <summary>Local HH:mm schedule values, preserved without conversion or live status calculations.</summary>
public sealed record OpeningInterval
{
    public required string OpensAt { get; init; }
    public required string ClosesAt { get; init; }
}
