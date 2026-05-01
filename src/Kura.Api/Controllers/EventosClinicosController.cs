namespace Kura.Api.Controllers;

using Kura.Application.DTOs.EventoClinico;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/eventos-clinicos")]
public class EventosClinicosController : ControllerBase
{
    private readonly IEventoClinicoService _eventoService;

    public EventosClinicosController(IEventoClinicoService eventoService)
    {
        _eventoService = eventoService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventoClinicoResponseDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? petId,
        [FromQuery] string? tipo,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] long? veterinarioId)
    {
        var result = await _eventoService.GetByFiltersAsync(petId, tipo, dataInicio, dataFim, veterinarioId);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(EventoClinicoResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _eventoService.GetByIdAsync(id);
        return Ok(result);
    }
}
