namespace Kura.Api.Controllers;

using Kura.Application.DTOs.EventoClinico;
using Kura.Application.DTOs.Exame;
using Kura.Application.DTOs.Prescricao;
using Kura.Application.DTOs.Vacina;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/eventos-clinicos")]
public class EventosClinicosController : ControllerBase
{
    private readonly IEventoClinicoService _eventoService;
    private readonly IVacinaService _vacinaService;
    private readonly IPrescricaoService _prescricaoService;
    private readonly IExameService _exameService;

    public EventosClinicosController(
        IEventoClinicoService eventoService,
        IVacinaService vacinaService,
        IPrescricaoService prescricaoService,
        IExameService exameService)
    {
        _eventoService = eventoService;
        _vacinaService = vacinaService;
        _prescricaoService = prescricaoService;
        _exameService = exameService;
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

    [HttpPost("vacinas")]
    [ProducesResponseType(typeof(VacinaResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateVacina([FromBody] VacinaCreateDto dto)
    {
        var result = await _vacinaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.IdEventoClinico }, result);
    }

    [HttpPost("prescricoes")]
    [ProducesResponseType(typeof(PrescricaoResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreatePrescricao([FromBody] PrescricaoCreateDto dto)
    {
        var result = await _prescricaoService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.IdEventoClinico }, result);
    }

    [HttpPost("exames")]
    [ProducesResponseType(typeof(ExameResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateExame([FromBody] ExameCreateDto dto)
    {
        var result = await _exameService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.IdEventoClinico }, result);
    }
}
