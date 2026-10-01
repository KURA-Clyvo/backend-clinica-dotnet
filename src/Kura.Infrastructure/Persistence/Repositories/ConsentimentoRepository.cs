namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class ConsentimentoRepository(KuraDbContext context) : IConsentimentoRepository
{
    /// <summary>
    /// REC-15, fix wave G2 (achado I-1, LGPD): antes desta task, esta consulta pegava
    /// UMA linha por <c>DtConsentimento</c> desc, sem desempate secundário — num
    /// empate exato de <c>DT_ACEITE</c>, o Oracle devolve uma linha ARBITRÁRIA.
    ///
    /// A view <c>VW_VACINAS_VENCENDO</c> (backend-tutor-java,
    /// db/migration-oracle/V21__vacinas_v2_notificacao_triagem_luna.sql, linhas 67-78 o
    /// comentário normativo e 125-136 a subquery — ancorado em 2026-09-30) resolve o
    /// MESMO empate com <c>MIN(CASE WHEN ST_ACEITO='S' AND DT_REVOGACAO IS NULL THEN 'S'
    /// ELSE 'N' END)</c> sobre TODAS as linhas empatadas — ou seja, "'S' só se TODAS as
    /// linhas empatadas forem aceite-e-não-revogado; UMA linha ruim no empate já vira
    /// 'N'". O comentário da própria view diz que isto é correção de um G2 anterior
    /// (LU-02): "'S' > 'N' lexicamente, o empate favorecia o ENVIO — errado para LGPD".
    ///
    /// <see cref="ThenBy"/> abaixo replica a MESMA prioridade sem precisar agregar: entre
    /// linhas empatadas em <c>DtConsentimento</c>, uma linha "ruim" (recusada OU
    /// revogada, chave 0) sempre vem ANTES de uma linha "boa" (chave 1) — então
    /// <c>FirstOrDefaultAsync</c> devolve a ruim sempre que ela existir entre as
    /// empatadas, non-deterministicamente só quando TODAS as empatadas forem boas (caso
    /// em que a identidade da linha não importa, o resultado é o mesmo). Método
    /// COMPARTILHADO com <see cref="Kura.Application.Services.TeleconsultaService"/> — o
    /// fix beneficia os dois consumidores, que é o comportamento LGPD-safe correto para
    /// os dois.
    /// </summary>
    public Task<Consentimento?> GetMaisRecenteAsync(long idTutor, string dsTipo)
        => context.Consentimentos
            .Where(c => c.IdTutor == idTutor && c.DsTipo == dsTipo)
            .OrderByDescending(c => c.DtConsentimento)
            .ThenBy(c => c.StAceito == 'S' && c.DtRevogacao == null ? 1 : 0)
            .FirstOrDefaultAsync();
}
