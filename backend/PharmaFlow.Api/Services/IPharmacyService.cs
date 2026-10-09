using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

public interface IPharmacyService
{
    IReadOnlyList<PharmacyBranch> GetAll();
    PharmacyBranch? GetBySlug(string slug);
}
