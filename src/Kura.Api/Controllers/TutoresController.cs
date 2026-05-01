namespace Kura.Api.Controllers;

using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/v1/tutores")]
public class TutoresController : ControllerBase
{
    private readonly ITutorService _service;

    public TutoresController(ITutorService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TutorResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] string? busca)
    {
        var result = await _service.SearchAsync(busca);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(TutorResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpGet("{id:long}/pets")]
    [ProducesResponseType(typeof(IEnumerable<PetResponseDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPets(long id)
    {
        var result = await _service.GetPetsAsync(id);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TutorResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] TutorCreateDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(TutorResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(long id, [FromBody] TutorUpdateDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return Ok(result);
    }
}
