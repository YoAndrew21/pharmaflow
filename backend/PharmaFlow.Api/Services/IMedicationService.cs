using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

public interface IMedicationService
{
    IReadOnlyList<Medication> GetAll();
    Medication? GetBySlug(string slug);
}
