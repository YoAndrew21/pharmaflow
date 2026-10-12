namespace PharmaFlow.Api.Persistence.Entities;

public sealed class MedicationEntity
{
    public required string Id { get; set; }
    public required string Slug { get; set; }
    public required string DisplayName { get; set; }
    public required string StrengthLabel { get; set; }
    public required string FormCode { get; set; }
    public required string PresentationLabel { get; set; }
    public required string CategoryCode { get; set; }
    public bool PrescriptionRequired { get; set; }
    public ICollection<MedicationActiveIngredientEntity> ActiveIngredients { get; set; } = new List<MedicationActiveIngredientEntity>();
    public ICollection<AvailabilityEntity> Availability { get; set; } = new List<AvailabilityEntity>();
}
