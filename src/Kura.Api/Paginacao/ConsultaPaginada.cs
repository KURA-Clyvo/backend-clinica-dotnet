namespace Kura.Api.Paginacao;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Parâmetros comuns de listas paginadas (S4-NET-03; reaproveitados pela S4-NET-04).
/// <c>page</c> ≥ 1 (padrão 1), <c>pageSize</c> 1..100 (padrão 20), <c>sort</c> opcional validado
/// contra uma LISTA BRANCA por recurso (<see cref="OrdenacaoPermitida{TEnum}"/>). Valor inválido
/// ⇒ 400 de validação (o <c>[ApiController]</c> converte o ModelState), nunca 500.
/// </summary>
public class ConsultaPaginada
{
    /// <summary>Teto de página: mantém <c>(page-1) * pageSize</c> dentro de <c>int</c> (o driver só aceita Skip int).</summary>
    public const int PaginaMaxima = 1_000_000;
    public const int TamanhoPaginaPadrao = 20;
    public const int TamanhoPaginaMaximo = 100;

    /// <summary>Página, base 1.</summary>
    [Range(1, PaginaMaxima, ErrorMessage = "'page' deve estar entre 1 e 1000000.")]
    public int Page { get; set; } = 1;

    /// <summary>Itens por página (1 a 100).</summary>
    [Range(1, TamanhoPaginaMaximo, ErrorMessage = "'pageSize' deve estar entre 1 e 100.")]
    public int PageSize { get; set; } = TamanhoPaginaPadrao;

    /// <summary>Ordenação; valores aceitos dependem do recurso. Prefixo <c>-</c> = decrescente.</summary>
    public string? Sort { get; set; }
}

/// <summary>
/// Lista branca de ordenação de um recurso: texto do cliente → enum FECHADO. Nenhuma string do
/// cliente chega ao driver ou ao LINQ; o que não está no mapa é recusado.
/// </summary>
public sealed class OrdenacaoPermitida<TEnum> where TEnum : struct, Enum
{
    private readonly Dictionary<string, TEnum> _mapa;

    public OrdenacaoPermitida(IReadOnlyDictionary<string, TEnum> mapa, TEnum padrao)
    {
        _mapa = new Dictionary<string, TEnum>(mapa, StringComparer.OrdinalIgnoreCase);
        Padrao = padrao;
    }

    public TEnum Padrao { get; }

    public IReadOnlyCollection<string> Permitidos => _mapa.Keys;

    /// <summary>Vazio/nulo ⇒ padrão. Fora da lista ⇒ <see langword="false"/>.</summary>
    public bool TentarResolver(string? sort, out TEnum ordem)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            ordem = Padrao;
            return true;
        }
        return _mapa.TryGetValue(sort.Trim(), out ordem);
    }
}
