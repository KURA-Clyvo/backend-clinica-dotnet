namespace Kura.Api.Controllers;

using Kura.Api.Hal;
using Kura.Api.Paginacao;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Paginacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Filtros da lista v2 de pets (somados a <c>page</c>, <c>pageSize</c> e <c>sort</c>).</summary>
public sealed class PetListaQuery : ConsultaPaginada
{
    /// <summary>Nome do pet: contém, sem distinção de maiúsculas/minúsculas.</summary>
    public string? Nome { get; set; }

    /// <summary>Somente pets vinculados a este tutor.</summary>
    public long? TutorId { get; set; }

    /// <summary>Somente pets desta espécie.</summary>
    public long? EspecieId { get; set; }

    /// <summary>Porte: P, M ou G.</summary>
    public string? Porte { get; set; }
}

/// <summary>
/// S4-NET-04 — pets em <c>/api/v2</c>: lista paginada/ordenada/filtrada no banco e detalhe, em HAL.
/// A v1 (<c>/api/v1/pets</c>, array puro) continua intacta (D-NET-4). Escopo: sempre a clínica do JWT;
/// pet de outra clínica, inexistente ou inativo dão a mesma resposta (404 no detalhe, ausência na lista).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v2/pets")]
public class PetsV2Controller : ControllerBase
{
    /// <summary>Lista branca de ordenação: sem sinal = crescente, <c>-</c> = decrescente.</summary>
    public static readonly OrdenacaoPermitida<OrdenacaoPet> Ordenacoes = new(
        new Dictionary<string, OrdenacaoPet>
        {
            ["nome"] = OrdenacaoPet.NomeAsc,
            ["-nome"] = OrdenacaoPet.NomeDesc,
            ["dtNascimento"] = OrdenacaoPet.DtNascimentoAsc,
            ["-dtNascimento"] = OrdenacaoPet.DtNascimentoDesc,
            ["id"] = OrdenacaoPet.IdAsc,
            ["-id"] = OrdenacaoPet.IdDesc,
        },
        padrao: OrdenacaoPet.NomeAsc);

    private readonly IPetService _service;
    private readonly IHalLinkFactory _links;
    private readonly ListasV2HalMapper _mapper;

    public PetsV2Controller(IPetService service, IHalLinkFactory links, ListasV2HalMapper mapper)
    {
        _service = service;
        _links = links;
        _mapper = mapper;
    }

    /// <summary>Lista pets (paginado, ordenado, filtrado).</summary>
    /// <param name="query">
    /// <c>page</c> (≥ 1, padrão 1), <c>pageSize</c> (1 a 100, padrão 20),
    /// <c>sort</c> (<c>nome</c> [padrão], <c>-nome</c>, <c>dtNascimento</c>, <c>-dtNascimento</c>, <c>id</c>, <c>-id</c>),
    /// <c>nome</c> (contém, sem caixa), <c>tutorId</c>, <c>especieId</c>, <c>porte</c> (P, M ou G).
    /// </param>
    /// <remarks>
    /// Exemplo: <c>GET /api/v2/pets?pageSize=2&amp;sort=-dtNascimento&amp;porte=M&amp;nome=rex</c>.
    /// Resposta 200:
    /// <code>
    /// { "items": [ { "id": 12, "nmPet": "Rex", "sgPorte": "M",
    ///     "_links": { "self": { "href": "/api/v2/pets/12" }, "laudos": { "href": "/api/v1/pets/12/laudos" },
    ///                 "editar": { "href": "/api/v1/pets/12", "method": "PUT" } } } ],
    ///   "page": 1, "pageSize": 2, "total": 1, "totalPages": 1,
    ///   "_links": { "self": { "href": "/api/v2/pets?..." }, "first": { "href": "..." }, "last": { "href": "..." } } }
    /// </code>
    /// A busca por <c>nome</c> é feita com <c>LOWER(...) LIKE</c> (sem caixa) no banco.
    /// </remarks>
    /// <response code="200">Página de pets.</response>
    /// <response code="400"><c>page</c>/<c>pageSize</c>/<c>sort</c>/<c>porte</c> inválido.</response>
    [HttpGet(Name = RotasV2.ListarPets)]
    [ProducesResponseType(typeof(PaginaHalDto<PetHalDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<IActionResult> Listar([FromQuery] PetListaQuery query)
    {
        if (!Ordenacoes.TentarResolver(query.Sort, out var ordem))
            ModelState.AddModelError(nameof(query.Sort),
                $"'sort' deve ser um de: {string.Join(", ", Ordenacoes.Permitidos)}.");

        char? porte = null;
        if (!string.IsNullOrWhiteSpace(query.Porte))
        {
            var p = query.Porte.Trim().ToUpperInvariant();
            if (p is "P" or "M" or "G") porte = p[0];
            else ModelState.AddModelError(nameof(query.Porte), "'porte' deve ser P, M ou G.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var nome = string.IsNullOrWhiteSpace(query.Nome) ? null : query.Nome.Trim();
        var pagina = await _service.ListarPaginadoAsync(
            new ConsultaPets(query.Page, query.PageSize, ordem, nome, query.TutorId, query.EspecieId, porte));

        var itens = pagina.Itens.Select(_mapper.Mapear).ToList();
        var filtros = new Dictionary<string, object?>
        {
            ["sort"] = string.IsNullOrWhiteSpace(query.Sort) ? null : query.Sort.Trim(),
            ["nome"] = nome,
            ["tutorId"] = query.TutorId,
            ["especieId"] = query.EspecieId,
            ["porte"] = porte?.ToString(),
        };
        return Ok(_links.MontarPagina(itens, pagina.Pagina, pagina.TamanhoPagina, pagina.Total, RotasV2.ListarPets, filtros));
    }

    /// <summary>Detalhe de um pet.</summary>
    /// <param name="id">Identificador do pet.</param>
    /// <response code="200">Pet encontrado (com <c>_links</c>).</response>
    /// <response code="404">Inexistente, inativo ou de outra clínica.</response>
    [HttpGet("{id:long}", Name = RotasV2.ObterPet)]
    [ProducesResponseType(typeof(PetHalDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Obter(long id) =>
        Ok(_mapper.Mapear(await _service.GetByIdAsync(id)));
}
