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

    /// <summary>
    /// REC-10 — busca por PK com <c>ID_CLINICA</c> EXPLÍCITO no predicado (A-7), para
    /// <c>POST /api/v1/agendamentos</c> (F-3, <c>idTriagemOrigem</c>). Devolve
    /// <see langword="null"/> tanto para id inexistente quanto para triagem de outra
    /// clínica — mesma resposta, sem oráculo. NÃO verifica o tutor: quem chama
    /// (<c>AgendaService.CriarAsync</c>) compara <c>TriagemLuna.IdTutor</c> com o tutor do
    /// corpo à parte, porque "triagem de outro tutor da MESMA clínica" tem resposta
    /// diferente (422) de "triagem inexistente/de outra clínica" (404) — G2/m-4.
    /// </summary>
    Task<TriagemLuna?> GetByIdAsync(long id, long idClinica);
}
