namespace Kura.Api.Controllers;

using System.Globalization;
using Kura.Api.Hal;
using Kura.Api.Paginacao;
using Kura.Application.Services;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Paginacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Filtros da lista v2 de agendamentos (somados a <c>page</c>, <c>pageSize</c> e <c>sort</c>).</summary>
public sealed class AgendamentoListaQuery : ConsultaPaginada
{
    /// <summary>Início do intervalo (ISO-8601, inclusivo, por dia). Obrigatório.</summary>
    public DateTime? DataInicio { get; set; }

    /// <summary>Fim do intervalo (ISO-8601, inclusivo, por dia; no máximo 31 dias após o início). Obrigatório.</summary>
    public DateTime? DataFim { get; set; }

    /// <summary>Somente agendamentos deste veterinário.</summary>
    public long? VeterinarioId { get; set; }

    /// <summary>Status: INTENCAO, AGENDADO, CONFIRMADO, REALIZADO, CANCELADO ou NAO_COMPARECEU.</summary>
    public string? Status { get; set; }
}

/// <summary>
/// S4-NET-04 — agendamentos em <c>/api/v2</c>. Sempre da clínica do JWT (a tabela não tem query
/// filter; o predicado de clínica é explícito no repositório). As ações de recepção/status em
/// <c>_links</c> só aparecem quando a regra do <c>AgendaService</c> as admite para o estado atual.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v2/agendamentos")]
public class AgendamentosV2Controller : ControllerBase
{
    /// <summary>Lista branca de ordenação: sem sinal = crescente, <c>-</c> = decrescente.</summary>
    public static readonly OrdenacaoPermitida<OrdenacaoAgendamento> Ordenacoes = new(
        new Dictionary<string, OrdenacaoAgendamento>
        {
            ["dtAgendamento"] = OrdenacaoAgendamento.DtAgendamentoAsc,
            ["-dtAgendamento"] = OrdenacaoAgendamento.DtAgendamentoDesc,
            ["id"] = OrdenacaoAgendamento.IdAsc,
            ["-id"] = OrdenacaoAgendamento.IdDesc,
        },
        padrao: OrdenacaoAgendamento.DtAgendamentoAsc);

    private const string FormatoData = "yyyy-MM-dd'T'HH:mm:ss";

    private readonly IAgendaService _service;
    private readonly IHalLinkFactory _links;
    private readonly ListasV2HalMapper _mapper;

    public AgendamentosV2Controller(IAgendaService service, IHalLinkFactory links, ListasV2HalMapper mapper)
    {
        _service = service;
        _links = links;
        _mapper = mapper;
    }

    /// <summary>Lista os agendamentos de um intervalo (paginado, ordenado, filtrado).</summary>
    /// <param name="query">
    /// <c>dataInicio</c> e <c>dataFim</c> (obrigatórios, até 31 dias), <c>page</c> (≥ 1, padrão 1),
    /// <c>pageSize</c> (1 a 100, padrão 20), <c>sort</c> (<c>dtAgendamento</c> [padrão], <c>-dtAgendamento</c>,
    /// <c>id</c>, <c>-id</c>), <c>veterinarioId</c>, <c>status</c>.
    /// </param>
    /// <remarks>
    /// Exemplo: <c>GET /api/v2/agendamentos?dataInicio=2026-10-26&amp;dataFim=2026-11-01&amp;status=AGENDADO&amp;sort=-dtAgendamento</c>.
    /// Em cada item, <c>_links</c> traz <c>pet</c> e, conforme o estado, <c>checkin</c>, <c>inicio-atendimento</c> e
    /// <c>status</c> (rotas v1 de ação). Não há rota v1 de detalhe de agendamento, então o item não tem <c>self</c>.
    /// </remarks>
    /// <response code="200">Página de agendamentos.</response>
    /// <response code="400">Intervalo ausente/malformado, <c>page</c>/<c>pageSize</c>/<c>sort</c>/<c>status</c> inválido.</response>
    /// <response code="422">Intervalo invertido ou superior a 31 dias.</response>
    [HttpGet(Name = RotasV2.ListarAgendamentos)]
    [ProducesResponseType(typeof(PaginaHalDto<AgendamentoHalDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> Listar([FromQuery] AgendamentoListaQuery query)
    {
        if (!Ordenacoes.TentarResolver(query.Sort, out var ordem))
            ModelState.AddModelError(nameof(query.Sort),
                $"'sort' deve ser um de: {string.Join(", ", Ordenacoes.Permitidos)}.");

        if (query.DataInicio is null)
            ModelState.AddModelError(nameof(query.DataInicio), "'dataInicio' é obrigatório (ISO-8601).");
        if (query.DataFim is null)
            ModelState.AddModelError(nameof(query.DataFim), "'dataFim' é obrigatório (ISO-8601).");

        string? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var s = query.Status.Trim().ToUpperInvariant();
            if (AgendaService.StatusConhecidos.Contains(s)) status = s;
            else ModelState.AddModelError(nameof(query.Status),
                $"'status' deve ser um de: {string.Join(", ", AgendaService.StatusConhecidos)}.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var inicio = query.DataInicio!.Value;
        var fim = query.DataFim!.Value;
        var pagina = await _service.ListarPaginadoAsync(new ConsultaAgendamentos(
            inicio, fim, query.Page, query.PageSize, ordem, query.VeterinarioId, status));

        var itens = pagina.Itens.Select(_mapper.Mapear).ToList();
        var filtros = new Dictionary<string, object?>
        {
            ["dataInicio"] = inicio.ToString(FormatoData, CultureInfo.InvariantCulture),
            ["dataFim"] = fim.ToString(FormatoData, CultureInfo.InvariantCulture),
            ["sort"] = string.IsNullOrWhiteSpace(query.Sort) ? null : query.Sort.Trim(),
            ["veterinarioId"] = query.VeterinarioId,
            ["status"] = status,
        };
        return Ok(_links.MontarPagina(itens, pagina.Pagina, pagina.TamanhoPagina, pagina.Total, RotasV2.ListarAgendamentos, filtros));
    }
}
