namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface ITutorRepository : IRepository<Tutor>
{
    /// <summary>
    /// Busca tutores da clínica informada, opcionalmente filtrando por nome/CPF.
    /// TASK-21: idClinica é obrigatório — defesa em profundidade além do HasQueryFilter
    /// global do DbContext, que desliga inteiro (não nega) quando não há contexto de clínica.
    /// </summary>
    Task<IEnumerable<Tutor>> SearchAsync(string? busca, long idClinica);

    /// <summary>
    /// Busca um tutor por Id, restrito à clínica informada. Retorna null se o tutor
    /// não existir ou pertencer a outra clínica.
    /// </summary>
    Task<Tutor?> GetByIdAsync(long id, long idClinica);

    /// <summary>
    /// TASK-67: busca por telefone, deliberadamente SEM escopo de clínica — consumida
    /// pela IA Luna (GET /api/v1/tutores/telefone/{numero}), que não tem JWT de clínica
    /// e é justamente quem precisa descobrir a clínica a partir do telefone. Diferente
    /// de SearchAsync/GetByIdAsync(id, idClinica) acima, que assumem contexto de clínica
    /// já resolvido. O EF ainda aplica o HasQueryFilter de StAtiva (soft delete) — a
    /// parte de tenant do filtro fica inerte porque a chamada não tem
    /// IClinicaContext.IdClinicaFiltro (sem JWT), não porque foi ignorada de propósito.
    ///
    /// TASK-79: TUTOR.DS_TELEFONE não tem UNIQUE, então mais de um tutor ATIVO pode
    /// compartilhar o mesmo número — inclusive dois tutores da MESMA clínica (ex.:
    /// casal com o telefone da casa), não só entre clínicas diferentes. Telefone
    /// ambíguo (>1 resultado, qualquer clínica) é tratado como "não encontrado"
    /// (null), a mesma forma já usada para telefone inexistente. Consequência real da
    /// colisão intra-clínica: aquele domicílio recebe o fallback genérico da Luna e a
    /// interação é gravada com ID_CLINICA/ID_TUTOR nulos — decisão mantida de
    /// propósito (ver TutorRepository.GetByTelefoneAsync para o raciocínio completo).
    /// </summary>
    Task<Tutor?> GetByTelefoneAsync(string numero);

    /// <summary>
    /// R3a (G2b fix wave 2, achado Minor — REC-01): conta tutores ATIVOS com <c>numero</c>
    /// exato em <c>DS_TELEFONE</c> — 0, 1 ou 2 (capado, mesmo raciocínio de TASK-79: só
    /// precisamos distinguir "não encontrado" de "ambíguo", nunca o total exato de colisões).
    ///
    /// <para>Existe porque <see cref="GetByTelefoneAsync"/> devolve <see langword="null"/> para
    /// AMBOS os casos (0 e 2+), e <c>TutorService.BuscarContextoPorTelefoneAsync</c> precisa
    /// distinguir os dois ANTES de decidir se tenta uma segunda chave de busca (prefixo
    /// nacional "55"): TASK-79 tem que valer sobre o RESULTADO FINAL da busca — uma tentativa
    /// exata AMBÍGUA já é "não encontrado" e NÃO deve cair na segunda tentativa, que poderia
    /// achar um TERCEIRO tutor completamente diferente (achado original: dois tutores
    /// colidentes no valor exato + um terceiro, distinto, sob o valor com prefixo).</para>
    /// </summary>
    Task<int> ContarAtivosPorTelefoneAsync(string numero);
}
