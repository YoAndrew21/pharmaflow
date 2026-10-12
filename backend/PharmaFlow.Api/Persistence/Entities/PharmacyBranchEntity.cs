namespace PharmaFlow.Api.Persistence.Entities;

public sealed class PharmacyBranchEntity
{
    public required string Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string FictionalLocation { get; set; }
    public required string TimeZone { get; set; }
    public ICollection<OpeningIntervalEntity> OpeningIntervals { get; set; } = new List<OpeningIntervalEntity>();
    public ICollection<AvailabilityEntity> Availability { get; set; } = new List<AvailabilityEntity>();
}
