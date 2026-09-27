namespace Kura.Domain.Interfaces;

/// <summary>
/// REC-08 (backlog <c>KURA_BACKLOG_RECEPCAO.md</c>, A-5/F-4) — fonte ÚNICA de "agora" e "hoje"
/// para tudo que grava ou lê <c>AGENDAMENTO.DT_AGENDAMENTO</c> e colunas irmãs da mesma tabela
/// (<c>DT_INICIO_SESSAO</c>, e o que a V23 vier a criar: <c>DT_CHECKIN</c>,
/// <c>DT_INICIO_ATENDIMENTO</c>).
///
/// <para><b>Por quê:</b> <c>Agendamento.java</c> e a <c>VW_VACINAS_VENCENDO</c> (V21) já tratam
/// essa coluna como hora LOCAL de São Paulo, sem conversão — é o Java quem grava o
/// <c>LocalDateTime</c> recebido cru. O <c>.NET</c> comparava com <c>DateTime.UtcNow</c>, o que
/// fazia a agenda "perder" as últimas ~3h de cada dia (<c>E8</c>, provado ao vivo no G0 item 3:
/// um agendamento das 11h desaparecia de "próximos" às 09:37 BRT). <c>Agora()</c>/<c>Hoje()</c>
/// devolvem SEMPRE hora local (fuso configurável, default <c>America/Sao_Paulo</c>) — nunca UTC.
/// </para>
///
/// <para><b>Fora de <c>AGENDAMENTO</c> isto NÃO se aplica</b> — <c>EventoClinico.DtEvento</c> e
/// <c>Vacina.DtProximaDose</c> continuam comparados em UTC (mesmo defeito, tabelas diferentes,
/// fora do escopo da REC-08; ver <c>DashboardService.cs:35,94</c> e <c>VacinaService.cs:114</c>).
/// </para>
/// </summary>
public interface IRelogioClinica
{
    /// <summary>Instante atual em hora local (fuso configurado), com data e hora.</summary>
    DateTime Agora();

    /// <summary>Data de hoje em hora local (equivalente a <c>Agora().Date</c>).</summary>
    DateTime Hoje();
}
