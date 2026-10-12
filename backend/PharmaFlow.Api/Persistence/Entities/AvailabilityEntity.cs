namespace PharmaFlow.Api.Persistence.Entities;

// A missing row is unknown information, never an inferred unavailable observation.
public sealed class AvailabilityEntity
{
    public required string MedicationId { get; set; }
    public required string BranchId { get; set; }
    public required string Status { get; set; }
    public long? PriceMinorUnits { get; set; }
    public string? PriceCurrencyCode { get; set; }
    // Npgsql timestamptz requires DateTimeOffset values with a zero (UTC) offset.
    public DateTimeOffset UpdatedAt { get; set; }
    public MedicationEntity Medication { get; set; } = null!;
    public PharmacyBranchEntity PharmacyBranch { get; set; } = null!;
}
