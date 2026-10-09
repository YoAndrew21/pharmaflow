using PharmaFlow.Api.Models;

namespace PharmaFlow.Api.Services;

public interface IAvailabilityService
{
    IReadOnlyList<Availability> GetAll();
}
