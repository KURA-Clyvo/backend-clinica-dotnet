namespace Kura.Api.Controllers;

using System.Security.Claims;
using FluentValidation;
using Kura.Api.Extensions;
using Kura.Api.Filters;
using Kura.Api.Hal;
using Kura.Api.Paginacao;
using Kura.Application.DTOs.Laudo;
using Kura.Application.Laudos;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Laudos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Filtros da lista de laudos (somados a <c>page</c>, <c>pageSize</c> e <c>sort</c>).
/// </summary>
public sealed class LaudoListaQuery : ConsultaPaginada
{
    /// <summary>Tipo do laudo: HEMOGRAMA, BIOQUIMICO, IMAGEM, RECEITA ou OUTRO.</summary>
    [FromQuery(Name = "tipo")]
    public string? Tipo { get; set; }

    /// <summary>Início do período do exame (ISO-8601; sem offset = UTC). Inclusivo.</summary>
    [FromQuery(Name = "de")]
    public string? De { get; set; }

    /// <summary>Fim do período do exame (ISO-8601; sem offset = UTC). Inclusivo; para o dia inteiro envie 23:59:59.</summary>
    [FromQuery(Name = "ate")]
    public string? Ate { get; set; }
}

/// <summary>
/// S4-NET-03 — laudos de pets (metadados no MongoDB, arquivo no storage). Respostas em HAL
/// (<c>_links</c>); listas no envelope paginado <c>{ items, page, pageSize, total, totalPages, _links }</c>.
///
/// <para><b>Escopo:</b> tudo é da clínica do JWT. Laudo ou pet de outra clínica, id inexistente e id
/// malformado dão a MESMA resposta: 404.</para>
/// <para><b>Papéis:</b> qualquer usuário autenticado da clínica envia e lê; só o GESTOR exclui
/// (<c>SomenteGestor</c> no MÉTODO do DELETE, mesmo molde da FD-15).</para>
/// <para><b>Armazenamento fora do ar</b> ⇒ 503 com <c>codigo=LAUDOS_INDISPONIVEL</c>; recusa do
/// schema do Mongo ⇒ 422.</para>
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1")]
public class LaudosController : ControllerBase
{
    /// <summary>
    /// Limite do CORPO da requisição de upload: o arquivo (10 MiB, validado à parte) mais folga
    /// para os campos de texto e o framing do multipart.
    /// </summary>
    public const long LimiteCorpoUploadBytes = 10L * 1024 * 1024 + 256 * 1024;

    /// <summary>Lista branca de ordenação: sem sinal = crescente, <c>-</c> = decrescente.</summary>
    public static readonly OrdenacaoPermitida<OrdenacaoLaudo> Ordenacoes = new(
        new Dictionary<string, OrdenacaoLaudo>
        {
            ["dataExame"] = OrdenacaoLaudo.DataExameAsc,
            ["-dataExame"] = OrdenacaoLaudo.DataExameDesc,
            ["criadoEm"] = OrdenacaoLaudo.CriadoEmAsc,
            ["-criadoEm"] = OrdenacaoLaudo.CriadoEmDesc,
        },
        padrao: OrdenacaoLaudo.DataExameDesc);

    private readonly ILaudoService _service;
    private readonly IValidator<LaudoCriarDto> _validador;
    private readonly IHalLinkFactory _links;
    private readonly LaudoHalMapper _mapper;

    public LaudosController(
        ILaudoService service,
        IValidator<LaudoCriarDto> validador,
        IHalLinkFactory links,
        LaudoHalMapper mapper)
    {
        _service = service;
        _validador = validador;
        _links = links;
        _mapper = mapper;
    }

    /// <summary>Envia um laudo (PDF, JPEG, PNG ou WebP até 10 MiB) para o pet.</summary>
    /// <remarks>
    /// <c>multipart/form-data</c> com as partes: <c>arquivo</c> (obrigatória), <c>tipo</c>,
    /// <c>titulo</c>, <c>dataExame</c> (obrigatórias), <c>laboratorio</c>, <c>observacao</c>,
    /// <c>idEventoClinico</c>, <c>dados</c> (string com um objeto JSON; opcional).
    /// O formato do arquivo é decidido pelos primeiros bytes, nunca pela extensão ou Content-Type
    /// do cliente: um arquivo <c>.pdf</c> que não é PDF recebe 400.
    ///
    /// Exemplo de resposta 201:
    /// <code>
    /// { "id": "671f...", "idPet": 12, "tipo": "HEMOGRAMA", "titulo": "Hemograma completo",
    ///   "_links": { "self": { "href": "/api/v1/laudos/671f..." }, "excluir": { "href": "/api/v1/laudos/671f...", "method": "DELETE" } } }
    /// </code>
    /// </remarks>
    /// <param name="idPet">Id do pet (da clínica do token).</param>
    /// <response code="201">Laudo criado; <c>Location</c> aponta o detalhe.</response>
    /// <response code="400">Campo inválido ou arquivo que não é PDF/imagem.</response>
    /// <response code="403">Token sem permissão.</response>
    /// <response code="404">Pet inexistente ou de outra clínica.</response>
    /// <response code="413">Corpo acima do limite.</response>
    /// <response code="422">O armazenamento recusou o laudo (schema).</response>
    /// <response code="503">Armazenamento de laudos indisponível (<c>codigo=LAUDOS_INDISPONIVEL</c>).</response>
    [HttpPost("pets/{idPet:long}/laudos")]
    [RequestSizeLimit(LimiteCorpoUploadBytes)]
    [DesabilitaFormValueProviders]
    [DocumentaMultipart("arquivo", CamposObrigatorios = ["tipo", "titulo", "dataExame"],
        CamposOpcionais = ["laboratorio", "observacao", "idEventoClinico", "dados"])]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(LaudoHalDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(413)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> Criar(long idPet, CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);

        await using var arquivo = form.Files["arquivo"]?.OpenReadStream();
        var dto = new LaudoCriarDto
        {
            Tipo = form["tipo"],
            Titulo = form["titulo"],
            DataExame = form["dataExame"],
            Laboratorio = form["laboratorio"],
            Observacao = form["observacao"],
            IdEventoClinico = form["idEventoClinico"],
            Dados = form["dados"],
            Arquivo = arquivo,
        };

        var validacao = _validador.Validate(dto);
        if (!validacao.IsValid)
        {
            foreach (var erro in validacao.Errors)
                ModelState.AddModelError(erro.PropertyName, erro.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        var email = User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;
        var criado = await _service.CriarAsync(idPet, dto, email, ct);
        var hal = await _mapper.MapearAsync(criado, User);
        return CreatedAtRoute(RotasLaudo.Obter, new { id = criado.Id }, hal);
    }

    /// <summary>Lista os laudos do pet (paginado, ordenado, filtrado).</summary>
    /// <param name="idPet">Id do pet (da clínica do token).</param>
    /// <param name="query">
    /// <c>page</c> (≥ 1, padrão 1), <c>pageSize</c> (1 a 100, padrão 20),
    /// <c>sort</c> (<c>dataExame</c>, <c>-dataExame</c> [padrão], <c>criadoEm</c>, <c>-criadoEm</c>),
    /// <c>tipo</c>, <c>de</c>, <c>ate</c>.
    /// </param>
    /// <remarks>
    /// Exemplo: <c>GET /api/v1/pets/12/laudos?pageSize=2&amp;sort=-dataExame&amp;tipo=HEMOGRAMA</c>.
    /// O envelope traz <c>_links</c> com <c>self</c>, <c>first</c>, <c>last</c> e, quando existem,
    /// <c>prev</c> e <c>next</c> — todos preservando filtros e ordenação.
    /// </remarks>
    /// <response code="200">Página de laudos.</response>
    /// <response code="400"><c>page</c>/<c>pageSize</c>/<c>sort</c>/<c>tipo</c>/<c>de</c>/<c>ate</c> inválido.</response>
    /// <response code="404">Pet inexistente ou de outra clínica.</response>
    /// <response code="503">Armazenamento de laudos indisponível.</response>
    [HttpGet("pets/{idPet:long}/laudos", Name = RotasLaudo.ListarDoPet)]
    [ProducesResponseType(typeof(PaginaHalDto<LaudoHalDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> Listar(long idPet, [FromQuery] LaudoListaQuery query, CancellationToken ct)
    {
        if (!Ordenacoes.TentarResolver(query.Sort, out var ordem))
            ModelState.AddModelError(nameof(query.Sort),
                $"'sort' deve ser um de: {string.Join(", ", Ordenacoes.Permitidos)}.");

        TipoLaudo? tipo = null;
        if (!string.IsNullOrWhiteSpace(query.Tipo))
        {
            if (LaudoEntrada.TentarTipo(query.Tipo, out var t)) tipo = t;
            else ModelState.AddModelError(nameof(query.Tipo), "'tipo' deve ser HEMOGRAMA, BIOQUIMICO, IMAGEM, RECEITA ou OUTRO.");
        }

        DateTime? de = null, ate = null;
        if (!string.IsNullOrWhiteSpace(query.De))
        {
            if (LaudoEntrada.TentarData(query.De, out var d)) de = d;
            else ModelState.AddModelError(nameof(query.De), "'de' deve ser uma data ISO-8601.");
        }
        if (!string.IsNullOrWhiteSpace(query.Ate))
        {
            if (LaudoEntrada.TentarData(query.Ate, out var a)) ate = a;
            else ModelState.AddModelError(nameof(query.Ate), "'ate' deve ser uma data ISO-8601.");
        }
        if (de is not null && ate is not null && de > ate)
            ModelState.AddModelError(nameof(query.De), "'de' não pode ser posterior a 'ate'.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var pagina = await _service.ListarPorPetAsync(
            idPet, new LaudoListarDto(query.Page, query.PageSize, ordem, tipo, de, ate), ct);

        var itens = new List<LaudoHalDto>();
        foreach (var laudo in pagina.Items)
            itens.Add(await _mapper.MapearAsync(laudo, User));

        var filtros = new Dictionary<string, object?>
        {
            ["idPet"] = idPet,
            ["sort"] = string.IsNullOrWhiteSpace(query.Sort) ? null : query.Sort.Trim(),
            ["tipo"] = tipo?.ToString(),
            ["de"] = string.IsNullOrWhiteSpace(query.De) ? null : query.De.Trim(),
            ["ate"] = string.IsNullOrWhiteSpace(query.Ate) ? null : query.Ate.Trim(),
        };
        return Ok(_links.MontarPagina(itens, pagina.Page, pagina.PageSize, pagina.Total, RotasLaudo.ListarDoPet, filtros));
    }

    /// <summary>Detalhe de um laudo.</summary>
    /// <param name="id">ObjectId do laudo (24 caracteres hexadecimais).</param>
    /// <remarks>
    /// <c>_links</c>: <c>self</c>, <c>pet</c>, <c>laudosDoPet</c>, <c>arquivo</c> e, <b>só para GESTOR</b>,
    /// <c>excluir</c> (<c>method: DELETE</c>).
    /// </remarks>
    /// <response code="200">Laudo encontrado.</response>
    /// <response code="404">Inexistente, removido, de outra clínica ou id malformado.</response>
    /// <response code="503">Armazenamento de laudos indisponível.</response>
    [HttpGet("laudos/{id}", Name = RotasLaudo.Obter)]
    [ProducesResponseType(typeof(LaudoHalDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> Obter(string id, CancellationToken ct) =>
        Ok(await _mapper.MapearAsync(await _service.ObterAsync(id, ct), User));

    /// <summary>Baixa o arquivo do laudo.</summary>
    /// <param name="id">ObjectId do laudo.</param>
    /// <response code="200">Binário, com Content-Type do mapa fechado (PDF, JPEG, PNG ou WebP).</response>
    /// <response code="404">Laudo (ou arquivo) inexistente, ou de outra clínica.</response>
    /// <response code="503">Armazenamento de laudos indisponível.</response>
    [HttpGet("laudos/{id}/arquivo", Name = RotasLaudo.Arquivo)]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> ObterArquivo(string id, CancellationToken ct)
    {
        var arquivo = await _service.ObterArquivoAsync(id, ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        // Nome gerado do id (nunca o nome original do upload, que pode conter o nome do tutor).
        return File(arquivo.Conteudo, arquivo.ContentType, $"laudo-{id}.{arquivo.Extensao}");
    }

    /// <summary>Exclui o laudo (lógico). Somente GESTOR.</summary>
    /// <param name="id">ObjectId do laudo.</param>
    /// <response code="204">Excluído.</response>
    /// <response code="403">Usuário não é GESTOR.</response>
    /// <response code="404">Inexistente, já removido, de outra clínica ou id malformado.</response>
    /// <response code="503">Armazenamento de laudos indisponível.</response>
    [HttpDelete("laudos/{id}", Name = RotasLaudo.Excluir)]
    [Authorize(Policy = PoliticasAutorizacao.SomenteGestor)]
    [ProducesResponseType(204)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> Excluir(string id, CancellationToken ct)
    {
        await _service.ExcluirAsync(id, ct);
        return NoContent();
    }
}
