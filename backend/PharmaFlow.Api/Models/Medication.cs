namespace PharmaFlow.Api.Models;

/// <summary>One fictional catalog presentation, distinguished by strength, form and package.</summary>
public sealed record Medication
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> ActiveIngredients { get; init; }
    public required string StrengthLabel { get; init; }
    public required string FormCode { get; init; }
    public required string PresentationLabel { get; init; }
    public required string CategoryCode { get; init; }

    /// <summary>Illustrative catalog metadata, not a legal or clinical determination.</summary>
    public required bool PrescriptionRequired { get; init; }
}
