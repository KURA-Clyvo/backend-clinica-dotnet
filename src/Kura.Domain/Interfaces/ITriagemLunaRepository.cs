namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;
using Kura.Domain.ValueObjects;

public interface ITriagemLunaRepository
{
    Task<List<TriagemLuna>> GetByIntervaloAsync(DateTime dataInicio, DateTime dataFim);

    /// <summary>
    /// TASK-67: primeira escrita nesta tabela — POST /api/v1/luna/triage.
    /// </summary>
    Task AddAsync(TriagemLuna entidade);

    /// <summary>
    /// LU-08 — GET /api/v1/luna/triagens. <paramref name="idClinica"/> é OBRIGATÓRIO
    /// e explícito, mesmo padrão de defesa em profundidade de
    /// <c>ITutorRepository.SearchAsync/GetByIdAsync(id, idClinica)</c> — não delega ao
    /// HasQueryFilter global de KuraDbContext (que, sozinho, já isolaria sob JWT, mas
    /// o brief exige o predicado explícito no LINQ do join TRIAGEM_LUNA →
    /// INTERACAO_CANAL: ver o comentário histórico em
    /// LunaService.RegistrarTriagemAsync sobre esse join "virar vazamento real").
    /// Ordenação fixa: ALTA → MEDIA → BAIXA, depois DtTriagem mais recente primeiro —
    /// não é parâmetro.
    /// </summary>
    Task<(IReadOnlyList<TriagemListaItem> Itens, int Total)> ListarPorClinicaAsync(
        long idClinica,
        string? urgencia,
        DateTime? dataInicio,
        DateTime? dataFim,
        int page,
        int pageSize);
}
