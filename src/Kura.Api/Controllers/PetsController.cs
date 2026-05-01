namespace Kura.Api.Controllers;

using Kura.Application.DTOs.EventoClinico;
using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Vacina;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/pets")]
public class PetsController : ControllerBase
{
    private readonly IPetService _petService;
    private readonly IVacinaService _vacinaService;
    private readonly IEventoClinicoService _eventoService;

    public PetsController(
        IPetService petService,
        IVacinaService vacinaService,
        IEventoClinicoService eventoService)
    {
        _petService = petService;
        _vacinaService = vacinaService;
        _eventoService = eventoService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PetResponseDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? tutorId,
        [FromQuery] long? especieId,
        [FromQuery] char? porte)
    {
        var result = await _petService.GetByFiltersAsync(tutorId, especieId, porte);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(PetResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _petService.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PetResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] PetCreateDto dto)
    {
        var result = await _petService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(PetResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] PetUpdateDto dto)
    {
        var result = await _petService.UpdateAsync(id, dto);
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(long id)
    {
        await _petService.SoftDeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:long}/tutores")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdicionarTutor(long id, [FromBody] AdicionarTutorPetDto dto)
    {
        await _petService.AdicionarTutorAsync(id, dto);
        return NoContent();
    }

    [HttpGet("{id:long}/timeline")]
    [ProducesResponseType(typeof(IEnumerable<TimelineItemDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetTimeline(long id)
    {
        var result = await _eventoService.GetTimelineAsync(id);
        return Ok(result);
    }

    [HttpGet("{id:long}/proximas-vacinas")]
    [ProducesResponseType(typeof(IEnumerable<VacinaResponseDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProximasVacinas(long id)
    {
        var result = await _vacinaService.GetProximasVacinasAsync(id);
        return Ok(result);
    }
}
