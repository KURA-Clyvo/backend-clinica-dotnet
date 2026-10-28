namespace Kura.Api.Hal;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Fábrica de links HAL (camada Api, nunca Domain/Application). Nenhum href é escrito à mão:
/// todos saem de <see cref="LinkGenerator"/> a partir do NOME de uma rota. A decisão de que ações
/// aparecem para quem pede usa a MESMA política de autorização que protege a rota
/// (<see cref="IAuthorizationService"/>), então o link <c>excluir</c> e o <c>403</c> do DELETE não
/// podem divergir.
/// </summary>
public interface IHalLinkFactory
{
    /// <summary>Link para a rota nomeada (path relativo à raiz do site, com PathBase).</summary>
    LinkHal Criar(string nomeRota, object? valores, string? metodo = null);

    /// <summary>O usuário satisfaz a política? (decide se uma ação aparece em <c>_links</c>).</summary>
    Task<bool> PodeAsync(ClaimsPrincipal usuario, string politica);

    /// <summary>
    /// Monta o envelope paginado. <paramref name="filtros"/> são os parâmetros de consulta
    /// ENTENDIDOS (filtros, sort, mais valores de rota como <c>idPet</c>) — repassados a todos os
    /// links, para que paginar nunca perca filtro nem ordenação. <c>prev</c>/<c>next</c> só existem
    /// quando existem.
    ///
    /// <para><b>Total zero (G2-m5):</b> <c>totalPages=0</c> e o envelope traz só <c>self</c> e
    /// <c>first</c> — sem <c>last</c>, <c>prev</c> nem <c>next</c>, porque não há página real para
    /// apontar. A S4-NET-04 (listas v2) herda esta regra.</para>
    /// </summary>
    PaginaHalDto<T> MontarPagina<T>(
        IReadOnlyList<T> itens, int pagina, int tamanhoPagina, long total,
        string nomeRota, IReadOnlyDictionary<string, object?> filtros) where T : IRecursoHal;
}

public sealed class HalLinkFactory : IHalLinkFactory
{
    private readonly LinkGenerator _links;
    private readonly IHttpContextAccessor _http;
    private readonly IAuthorizationService _autorizacao;

    public HalLinkFactory(LinkGenerator links, IHttpContextAccessor http, IAuthorizationService autorizacao)
    {
        _links = links;
        _http = http;
        _autorizacao = autorizacao;
    }

    public LinkHal Criar(string nomeRota, object? valores, string? metodo = null)
    {
        var path = _links.GetPathByName(_http.HttpContext!, nomeRota, valores)
            ?? throw new InvalidOperationException($"Rota '{nomeRota}' não encontrada para gerar link HAL.");
        return new LinkHal(path, metodo);
    }

    public async Task<bool> PodeAsync(ClaimsPrincipal usuario, string politica) =>
        (await _autorizacao.AuthorizeAsync(usuario, null, politica)).Succeeded;

    public PaginaHalDto<T> MontarPagina<T>(
        IReadOnlyList<T> itens, int pagina, int tamanhoPagina, long total,
        string nomeRota, IReadOnlyDictionary<string, object?> filtros) where T : IRecursoHal
    {
        var totalPaginas = (int)Math.Min(int.MaxValue, (total + tamanhoPagina - 1) / tamanhoPagina);
        var ultima = Math.Max(1, totalPaginas);

        LinkHal Para(int p)
        {
            var valores = new RouteValueDictionary();
            foreach (var (chave, valor) in filtros)
                if (valor is not null) valores[chave] = valor;
            valores["page"] = p;
            valores["pageSize"] = tamanhoPagina;
            return Criar(nomeRota, valores);
        }

        var links = new Dictionary<string, LinkHal>
        {
            ["self"] = Para(pagina),
            ["first"] = Para(1),
        };
        // G2-m5: total=0 => totalPages=0 e SEM last (não existe última página para apontar).
        if (totalPaginas > 0) links["last"] = Para(ultima);
        if (pagina > 1 && totalPaginas > 0) links["prev"] = Para(Math.Min(pagina - 1, ultima));
        if (pagina < totalPaginas) links["next"] = Para(pagina + 1);

        return new PaginaHalDto<T>
        {
            Items = itens,
            Page = pagina,
            PageSize = tamanhoPagina,
            Total = total,
            TotalPages = totalPaginas,
            Links = links,
        };
    }
}
