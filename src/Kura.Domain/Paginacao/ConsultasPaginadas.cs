namespace Kura.Domain.Paginacao;

/// <summary>Uma página de resultado: itens da página + total que satisfaz o MESMO predicado da página.</summary>
public sealed record PaginaDe<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, long Total)
{
    /// <summary>Converte os itens mantendo a paginação e o total.</summary>
    public PaginaDe<TSaida> Mapear<TSaida>(Func<T, TSaida> f) =>
        new(Itens.Select(f).ToList(), Pagina, TamanhoPagina, Total);
}

/// <summary>Ordenações permitidas na lista de pets (lista branca fechada; o desempate final é sempre o Id).</summary>
public enum OrdenacaoPet { NomeAsc, NomeDesc, DtNascimentoAsc, DtNascimentoDesc, IdAsc, IdDesc }

/// <summary>Ordenações permitidas na lista de tutores.</summary>
public enum OrdenacaoTutor { NomeAsc, NomeDesc, IdAsc, IdDesc }

/// <summary>Ordenações permitidas na lista de agendamentos.</summary>
public enum OrdenacaoAgendamento { DtAgendamentoAsc, DtAgendamentoDesc, IdAsc, IdDesc }

/// <summary>Filtros da lista de pets. <c>Nome</c> = contém, sem distinção de caixa.</summary>
public sealed record ConsultaPets(
    int Pagina = 1,
    int TamanhoPagina = 20,
    OrdenacaoPet Ordem = OrdenacaoPet.NomeAsc,
    string? Nome = null,
    long? TutorId = null,
    long? EspecieId = null,
    char? Porte = null);

/// <summary>Filtro da lista de tutores. <c>Busca</c> = nome (contém, sem caixa) ou CPF parcial — igual à v1.</summary>
public sealed record ConsultaTutores(
    int Pagina = 1,
    int TamanhoPagina = 20,
    OrdenacaoTutor Ordem = OrdenacaoTutor.NomeAsc,
    string? Busca = null);

/// <summary>Filtros da lista de agendamentos. O intervalo é por DIA (fim inclusivo).</summary>
public sealed record ConsultaAgendamentos(
    DateTime DataInicio,
    DateTime DataFim,
    int Pagina = 1,
    int TamanhoPagina = 20,
    OrdenacaoAgendamento Ordem = OrdenacaoAgendamento.DtAgendamentoAsc,
    long? IdVeterinario = null,
    string? Status = null);
