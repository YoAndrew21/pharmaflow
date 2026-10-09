namespace PharmaFlow.Api.Models;

/// <summary>One recorded fictional medication/branch observation. Missing pairs remain absent.</summary>
public sealed record Availability
{
    public required string MedicationId { get; init; }
    public required string BranchId { get; init; }

    /// <summary>Canonical string: available, low-stock or unavailable; validated during loading.</summary>
    public required string Status { get; init; }

    /// <summary>Null means no reference price was provided, independently of stock status.</summary>
    public required ReferencePrice? ReferencePrice { get; init; }

    /// <summary>The original ISO 8601 UTC snapshot value, validated without reformatting.</summary>
    public required string UpdatedAt { get; init; }
}
