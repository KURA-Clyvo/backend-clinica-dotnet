namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;
using Kura.Domain.Paginacao;

public interface IAgendamentoReadRepository
{
    Task<IEnumerable<Agendamento>> GetByIntervaloAsync(
        long idClinica, DateTime dataInicio, DateTime dataFim, long? idVeterinario);

    /// <summary>
    /// S4-NET-04 — lista paginada da v2. <c>Agendamento</c> NÃO tem query filter: o predicado
    /// <c>IdClinica</c> é explícito aqui (é a lacuna que já vazou no DashboardService).
    /// </summary>
    Task<PaginaDe<Agendamento>> ListarPaginadoAsync(long idClinica, ConsultaAgendamentos consulta);
}
