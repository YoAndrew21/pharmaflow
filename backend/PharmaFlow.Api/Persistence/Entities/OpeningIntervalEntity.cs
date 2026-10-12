namespace PharmaFlow.Api.Persistence.Entities;

public sealed class OpeningIntervalEntity
{
    public required string PharmacyBranchId { get; set; }
    // ISO weekdays: Monday = 1 through Sunday = 7; no rows means the day is closed.
    public int DayOfWeek { get; set; }
    public int Position { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
    public PharmacyBranchEntity PharmacyBranch { get; set; } = null!;
}
