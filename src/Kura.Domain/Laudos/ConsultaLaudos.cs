namespace Kura.Domain.Laudos;

/// <summary>Ordenações permitidas na listagem de laudos (lista branca fechada; nunca string livre).</summary>
public enum OrdenacaoLaudo
{
    DataExameDesc,
    DataExameAsc,
    CriadoEmDesc,
    CriadoEmAsc,
    TituloAsc,
    TituloDesc,
}

/// <summary>Filtros, ordenação e paginação da lista de laudos de um pet. <see cref="Pagina"/> é base 1.</summary>
public sealed record ConsultaLaudos(
    int Pagina = 1,
    int TamanhoPagina = 20,
    TipoLaudo? Tipo = null,
    DateTime? DataDe = null,
    DateTime? DataAte = null,
    OrdenacaoLaudo Ordem = OrdenacaoLaudo.DataExameDesc);

public sealed record PaginaLaudos(IReadOnlyList<Laudo> Itens, int Pagina, int TamanhoPagina, long Total);
