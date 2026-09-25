namespace Kura.Api.Controllers;

using FluentValidation;
using Kura.Application.DTOs.EventoClinico;
using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Vacina;
using Kura.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Gerenciamento de pets, vínculos com tutores, timeline e carteira de vacinas.
///
/// <para><b><see cref="UploadFoto"/> (FT-03) usa só o <c>[Authorize]</c> da CLASSE, sem
/// policy</b> — decisão F2 do Felipe (backlog <c>KURA_BACKLOG_FOTO_PET.md</c>): GESTOR e
/// VETERINARIO podem subir foto de pet, e hoje esses são os ÚNICOS 2 papéis que existem
/// (<c>UsuarioClinica.cs:75-76</c>), então "qualquer autenticado" já É "GESTOR ou
/// VETERINARIO" — não há papel a excluir.</para>
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/pets")]
public class PetsController : ControllerBase
{
    private readonly IPetService _petService;
    private readonly IVacinaService _vacinaService;
    private readonly IEventoClinicoService _eventoService;
    private readonly IPetFotoService _petFotoService;
    private readonly IValidator<PetFotoUploadDto> _validadorFoto;

    public PetsController(
        IPetService petService,
        IVacinaService vacinaService,
        IEventoClinicoService eventoService,
        IPetFotoService petFotoService,
        IValidator<PetFotoUploadDto> validadorFoto)
    {
        _petService = petService;
        _vacinaService = vacinaService;
        _eventoService = eventoService;
        _petFotoService = petFotoService;
        _validadorFoto = validadorFoto;
    }

    /// <summary>
    /// Lista pets com filtros opcionais por tutor, espécie e porte.
    /// </summary>
    /// <param name="tutorId">Filtrar pelos pets de um tutor (opcional).</param>
    /// <param name="especieId">Filtrar por espécie (opcional).</param>
    /// <param name="porte">Filtrar por porte: P, M ou G (opcional).</param>
    /// <returns>Lista de pets ativos conforme os filtros.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
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

    /// <summary>
    /// Busca um pet pelo ID.
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <returns>Dados do pet encontrado.</returns>
    /// <response code="200">Pet encontrado.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(PetResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _petService.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Cadastra um novo pet vinculado a um tutor existente.
    /// </summary>
    /// <param name="dto">Dados do pet a ser cadastrado.</param>
    /// <returns>Pet criado com vínculo tutor-pet.</returns>
    /// <response code="201">Pet criado com sucesso.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Tutor informado não encontrado.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PetResponseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Create([FromBody] PetCreateDto dto)
    {
        var result = await _petService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Atualiza os dados de um pet.
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <param name="dto">Dados atualizados do pet.</param>
    /// <returns>Pet com dados atualizados.</returns>
    /// <response code="200">Pet atualizado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(PetResponseDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Update(long id, [FromBody] PetUpdateDto dto)
    {
        var result = await _petService.UpdateAsync(id, dto);
        return Ok(result);
    }

    /// <summary>
    /// Inativa um pet (soft delete).
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <returns>Sem conteúdo.</returns>
    /// <response code="204">Pet inativado.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Delete(long id)
    {
        await _petService.SoftDeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Vincula um tutor adicional a um pet (N:N tutor-pet).
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <param name="dto">ID do tutor a vincular e indicador de tutor principal.</param>
    /// <returns>Sem conteúdo.</returns>
    /// <response code="204">Tutor vinculado com sucesso.</response>
    /// <response code="400">Dados inválidos ou vínculo duplicado.</response>
    /// <response code="404">Pet ou tutor não encontrado.</response>
    [HttpPost("{id:long}/tutores")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> AdicionarTutor(long id, [FromBody] AdicionarTutorPetDto dto)
    {
        await _petService.AdicionarTutorAsync(id, dto);
        return NoContent();
    }

    /// <summary>
    /// Retorna a timeline cronológica de eventos clínicos do pet (vacinas, prescrições, exames, consultas).
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <returns>Lista ordenada de eventos clínicos.</returns>
    /// <response code="200">Timeline retornada com sucesso.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpGet("{id:long}/timeline")]
    [ProducesResponseType(typeof(IEnumerable<TimelineItemDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetTimeline(long id)
    {
        var result = await _eventoService.GetTimelineAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Retorna as próximas vacinas agendadas para o pet.
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <returns>Lista de vacinas com próximas doses pendentes.</returns>
    /// <response code="200">Vacinas retornadas.</response>
    /// <response code="404">Pet não encontrado.</response>
    [HttpGet("{id:long}/proximas-vacinas")]
    [ProducesResponseType(typeof(IEnumerable<VacinaResponseDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetProximasVacinas(long id)
    {
        var result = await _vacinaService.GetProximasVacinasAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Sobe a foto do pet (2 variantes, geradas pelo cliente — FT-07): <c>thumb</c> (256px,
    /// lista/avatar) e <c>media</c> (1080px, detalhe). Tipo real validado por magic bytes
    /// (JPEG/PNG/WebP), nunca por <c>Content-Type</c> do cliente — FT-03/backlog
    /// <c>KURA_BACKLOG_FOTO_PET.md</c>, regra A4/"Validação no servidor". Troca de foto:
    /// grava as novas, atualiza a linha, só DEPOIS exclui as antigas.
    /// </summary>
    /// <param name="id">Identificador do pet.</param>
    /// <param name="ct">Token de cancelamento (encerra a leitura do corpo se a conexão cair).</param>
    /// <returns>Chave e data da foto gravada.</returns>
    /// <response code="200">Foto gravada com sucesso.</response>
    /// <response code="400">Parte ausente, vazia, ou bytes que não batem JPEG/PNG/WebP.</response>
    /// <response code="404">Pet não encontrado (ou pertence a outra clínica).</response>
    /// <response code="413">Corpo da requisição maior que o limite (2 MB).</response>
    /// <remarks>
    /// <para>🔴 <b>Fix wave G2 (g2-ft03.md, achado G2-e).</b> Este endpoint lê o multipart ELE
    /// MESMO (<see cref="HttpRequest.ReadFormAsync(CancellationToken)"/>) em vez de receber
    /// <c>[FromForm] PetFotoUploadDto</c> como parâmetro, porque <c>PetFotoUploadDto</c>
    /// deixou de ter <c>IFormFile</c>/<c>[FromForm]</c> — <c>Kura.Application</c> não pode
    /// depender de <c>Microsoft.AspNetCore.*</c>. A validação (magic bytes, presença,
    /// tamanho) roda manualmente aqui via <see cref="_validadorFoto"/>.</para>
    ///
    /// <para>🔴 <b>Fix wave G2 (g2-ft03.md, achado G2-c) — ONDE o 413 de verdade é
    /// produzido, e NÃO é aqui.</b> MEDIDO com diagnóstico (não deduzido): tirar
    /// <c>[FromForm]</c> desta assinatura NÃO evita o <c>FormValueProviderFactory</c> do MVC
    /// — ele chama <c>Request.ReadFormAsync()</c> para QUALQUER requisição
    /// <c>multipart/form-data</c> que chega a UM controller action, incondicionalmente
    /// (só olha <c>HttpRequest.HasFormContentType</c>, nunca os parâmetros da action), ANTES
    /// da action ser invocada. Confirmado com um log na primeira linha deste método: ele
    /// NUNCA é escrito quando o corpo excede o limite — a requisição morre antes de chegar
    /// aqui. O fix real está em <c>Program.cs</c>
    /// (<c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c>): quando o Kestrel recusa
    /// o corpo por <see cref="RequestSizeLimitAttribute"/>, o
    /// <see cref="Microsoft.AspNetCore.Http.BadHttpRequestException"/>(413) (herda de
    /// <see cref="IOException"/>) é capturado pelo <c>FormValueProviderFactory</c> e
    /// embrulhado em <c>ValueProviderException</c>, que vira erro de ModelState — SÓ A
    /// MENSAGEM sobrevive (<c>ModelError.Exception</c> é <see langword="null"/>, medido). O
    /// factory customizado reconhece o texto ("Request body too large", produzido só pelo
    /// Kestrel nesse cenário) e devolve 413 de verdade em vez do 400 automático. Provado com
    /// <c>UseKestrel()</c> real: corpo maior que o limite → 413; corpo válido menor que o
    /// limite → 200 (mesmo instrumento, controle positivo). Ver
    /// <c>PetFotoKestrelHttpTests</c> e o relatório da task.</para>
    /// </remarks>
    [HttpPost("{id:long}/foto")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PetFotoResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(413)]
    public async Task<IActionResult> UploadFoto(long id, CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);

        await using var streamThumb = form.Files["thumb"]?.OpenReadStream();
        await using var streamMedia = form.Files["media"]?.OpenReadStream();

        // Fix wave G2 (achado G2-e): PetFotoUploadDto deixou de ter IFormFile/[FromForm] —
        // Kura.Application não pode depender de ASP.NET. Este controller monta o DTO com
        // Stream puro e valida MANUALMENTE (a auto-validation do FluentValidation só reage a
        // parâmetro bound por atributo, e este endpoint parou de ter um).
        var dto = new PetFotoUploadDto { Thumb = streamThumb, Media = streamMedia };
        var validacao = _validadorFoto.Validate(dto);
        if (!validacao.IsValid)
        {
            foreach (var erro in validacao.Errors)
                ModelState.AddModelError(erro.PropertyName, erro.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var result = await _petFotoService.UploadFotoAsync(id, dto.Thumb!, dto.Media!, ct);
        return Ok(result);
    }
}
