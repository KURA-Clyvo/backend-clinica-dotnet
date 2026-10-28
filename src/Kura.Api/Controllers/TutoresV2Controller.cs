namespace Kura.Api.Controllers;

using Kura.Api.Hal;
using Kura.Api.Paginacao;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Paginacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Filtro da lista v2 de tutores (somado a <c>page</c>, <c>pageSize</c> e <c>sort</c>).</summary>
public sealed class TutorListaQuery : ConsultaPaginada
{
    /// <summary>Texto parcial de nome (sem caixa) ou de CPF — mesma semântica da v1.</summary>
    public string? Busca { get; set; }
}

/// <summary>
/// S4-NET-04 — tutores em <c>/api/v2</c>. A resposta NÃO expõe campo a mais que a v1.
///
/// <para><b>PII em <c>busca</c>:</b> o cliente pode buscar por CPF parcial. Os links de navegação
/// (<c>self</c>/<c>first</c>/<c>last</c>/<c>prev</c>/<c>next</c>) repetem o <c>busca</c> — omiti-lo
/// quebraria a paginação (a página 2 deixaria de ser a página 2 da mesma busca). Risco aceito e
/// documentado: o valor volta apenas para quem o enviou, dentro do corpo da resposta; o servidor
/// não o escreve em log nem em header (o Serilog de requisição usa o caminho, sem query string).
/// Limite do que foi medido: a instrumentação do OpenTelemetry pode anexar a query string ao span
/// (<c>url.query</c>); isso não foi verificado nesta task.</para>
/// </summary>
[Authorize]
[ApiController]
[Route("api/v2/tutores")]
public class TutoresV2Controller : ControllerBase
{
    /// <summary>Lista branca de ordenação: sem sinal = crescente, <c>-</c> = decrescente.</summary>
    public static readonly OrdenacaoPermitida<OrdenacaoTutor> Ordenacoes = new(
        new Dictionary<string, OrdenacaoTutor>
        {
            ["nome"] = OrdenacaoTutor.NomeAsc,
            ["-nome"] = OrdenacaoTutor.NomeDesc,
            ["id"] = OrdenacaoTutor.IdAsc,
            ["-id"] = OrdenacaoTutor.IdDesc,
        },
        padrao: OrdenacaoTutor.NomeAsc);

    private readonly ITutorService _service;
    private readonly IHalLinkFactory _links;
    private readonly ListasV2HalMapper _mapper;

    public TutoresV2Controller(ITutorService service, IHalLinkFactory links, ListasV2HalMapper mapper)
    {
        _service = service;
        _links = links;
        _mapper = mapper;
    }

    /// <summary>Lista tutores da clínica (paginado, ordenado, com busca).</summary>
    /// <param name="query">
    /// <c>page</c> (≥ 1, padrão 1), <c>pageSize</c> (1 a 100, padrão 20),
    /// <c>sort</c> (<c>nome</c> [padrão], <c>-nome</c>, <c>id</c>, <c>-id</c>), <c>busca</c> (nome ou CPF parcial).
    /// </param>
    /// <remarks>Exemplo: <c>GET /api/v2/tutores?pageSize=10&amp;sort=-nome&amp;busca=silva</c>.</remarks>
    /// <response code="200">Página de tutores.</response>
    /// <response code="400"><c>page</c>/<c>pageSize</c>/<c>sort</c> inválido.</response>
    [HttpGet(Name = RotasV2.ListarTutores)]
    [ProducesResponseType(typeof(PaginaHalDto<TutorHalDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<IActionResult> Listar([FromQuery] TutorListaQuery query)
    {
        if (!Ordenacoes.TentarResolver(query.Sort, out var ordem))
            ModelState.AddModelError(nameof(query.Sort),
                $"'sort' deve ser um de: {string.Join(", ", Ordenacoes.Permitidos)}.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var busca = string.IsNullOrWhiteSpace(query.Busca) ? null : query.Busca.Trim();
        var pagina = await _service.ListarPaginadoAsync(new ConsultaTutores(query.Page, query.PageSize, ordem, busca));

        var itens = pagina.Itens.Select(_mapper.Mapear).ToList();
        var filtros = new Dictionary<string, object?>
        {
            ["sort"] = string.IsNullOrWhiteSpace(query.Sort) ? null : query.Sort.Trim(),
            ["busca"] = busca,
        };
        return Ok(_links.MontarPagina(itens, pagina.Pagina, pagina.TamanhoPagina, pagina.Total, RotasV2.ListarTutores, filtros));
    }

    /// <summary>Detalhe de um tutor.</summary>
    /// <param name="id">Identificador do tutor.</param>
    /// <response code="200">Tutor encontrado (com <c>_links</c>).</response>
    /// <response code="404">Inexistente, inativo ou de outra clínica.</response>
    [HttpGet("{id:long}", Name = RotasV2.ObterTutor)]
    [ProducesResponseType(typeof(TutorHalDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Obter(long id) =>
        Ok(_mapper.Mapear(await _service.GetByIdAsync(id)));
}
