namespace PharmaFlow.Api.Models;

/// <summary>A fictional package reference price; no currency conversion or floating-point amount.</summary>
public sealed record ReferencePrice
{
    public required long MinorUnits { get; init; }
    public required string CurrencyCode { get; init; }
}
