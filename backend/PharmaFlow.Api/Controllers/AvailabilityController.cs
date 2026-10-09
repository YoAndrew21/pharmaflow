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
    public ActionResult<IReadOnlyList<Availability>> GetAll() => Ok(availabilityService.GetAll());
}
