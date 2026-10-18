using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Api.Models;
using PharmaFlow.Api.Services;

namespace PharmaFlow.Api.Controllers;

[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController(IAvailabilityService availabilityService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Availability>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Availability>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await availabilityService.GetAllAsync(cancellationToken));
}
