using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Api.Models;
using PharmaFlow.Api.Services;

namespace PharmaFlow.Api.Controllers;

[ApiController]
[Route("api/medications")]
public sealed class MedicationsController(IMedicationService medicationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Medication>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Medication>> GetAll() => Ok(medicationService.GetAll());

    [HttpGet("{slug}")]
    [ProducesResponseType<Medication>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<Medication> GetBySlug(string slug)
    {
        var medication = medicationService.GetBySlug(slug);
        if (medication is null)
        {
            return NotFound();
        }

        return Ok(medication);
    }
}
