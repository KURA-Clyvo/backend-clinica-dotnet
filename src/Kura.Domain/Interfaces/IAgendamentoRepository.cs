namespace Kura.Domain.Interfaces;

using Kura.Domain.Entities;

public interface IAgendamentoRepository
{
    /// <summary>
    /// FD-17 — <c>idClinica</c> passou a ser obrigatório aqui e em
    /// <see cref="GetRecentesAsync"/>. <c>Agendamento</c> é a única entidade fora de
    /// <c>KuraDbContext.ApplyTenantFilters</c> (allowlist de compensação manual), e estes 2
    /// métodos consultavam <c>_context.Agendamentos</c> sem nenhum predicado de clínica —
    /// vazamento cross-tenant real na primeira tela pós-login (dashboard). Corrigido seguindo
    /// o mesmo padrão já usado em <see cref="GetByIdAsync"/> e em
    /// <c>AgendaService.cs</c>/<c>IAgendamentoReadRepository.GetByIntervaloAsync</c>: o
    /// consumidor (<c>DashboardService</c>) lê <c>IClinicaContext.IdClinica</c> e passa
    /// explicitamente — nunca confiar em filtro global aqui.
    /// </summary>
    /// <summary>
    /// REC-08 -- o parâmetro <c>agora</c> serve DUAS finalidades: casar o dia
    /// (<c>DtAgendamento.Date == agora.Date</c>) e o corte de futuro
    /// (<c>DtAgendamento >= agora</c>). Antes deste ajuste, o repositório usava
    /// <c>DateTime.UtcNow</c> internamente para o corte de futuro, ignorando este parâmetro --
    /// achado do G0 item 3 ("2º UtcNow escondido"). O chamador deve passar
    /// <c>IRelogioClinica.Agora()</c> (hora local de SP), nunca <c>DateTime.UtcNow</c>.
    /// </summary>
    Task<IEnumerable<Agendamento>> GetProximosDoDiaAsync(long idClinica, DateTime agora, int limite);
    Task<IEnumerable<Agendamento>> GetRecentesAsync(long idClinica, DateTime referencia, int limite);
    Task<Agendamento?> GetByIdAsync(long id, long idClinica);

    /// <summary>
    /// REC-15 — usado pelos 3 endpoints de confirmação D-1 consumidos pela Luna
    /// (API key, sem JWT de clínica — A-7 decide que a defesa aqui não é escopo de
    /// clínica, é o id_tutor do corpo batendo com o tutor do próprio agendamento,
    /// conferido no service). Inclui <c>Tutor</c> (para validar id_tutor sem round
    /// trip extra).
    /// </summary>
    Task<Agendamento?> GetByIdComTutorAsync(long id);

    /// <summary>
    /// REC-15 — candidatos a lembrete de confirmação D-1: ST_STATUS='AGENDADO', na
    /// data informada (hora local de SP, mesma convenção de DtAgendamento — A-5),
    /// ainda sem DT_LEMBRETE_CONFIRMACAO, com tutor vinculado E
    /// Tutor.DsWhatsapp preenchido. SEM escopo de clínica de propósito (A-7): é o job
    /// global de lembretes da Luna, que serve todas as clínicas numa passada só — ver
    /// decisão 2 do diário da REC-15. A checagem de consentimento (CONSENTIMENTO,
    /// DS_TIPO='LEMBRETES') NÃO é feita aqui — fica no service, via
    /// IConsentimentoRepository, porque não existe coluna de consentimento em TUTOR
    /// nem em AGENDAMENTO (achado 1 do diário).
    /// </summary>
    Task<IEnumerable<Agendamento>> GetConfirmacaoPendenteAsync(DateTime data);

    /// <summary>
    /// FD-17 — conta agendamentos de teleconsulta cuja sessão foi iniciada no dia informado,
    /// escopados por clínica (mesma razão de <see cref="GetProximosDoDiaAsync"/>: <c>Agendamento</c>
    /// não tem filtro global). "Hoje" aqui é <c>DT_INICIO_SESSAO</c>, não
    /// <c>DT_AGENDAMENTO</c> — ver decisão registrada em <c>DashboardService.GetHojeAsync</c>.
    ///
    /// <para>⚠️ <b>Limite semântico medido na G2 (não corrigir sem decisão de produto):</b>
    /// <c>DT_INICIO_SESSAO</c> é, pelo comentário da própria coluna na
    /// <c>V10__agendamento_teleconsulta.sql</c> do repo Java, <i>"Timestamp de criação da sala
    /// de videochamada"</i> — e <c>TeleconsultaService.CriarOuObterSalaAsync</c> tem
    /// early-return quando a sala já existe, então reabrir a sala no dia seguinte <b>não</b>
    /// atualiza o campo. Logo este contador conta <b>salas criadas hoje</b>, não sessões
    /// realizadas hoje: uma teleconsulta com sala criada ontem e conduzida hoje é contada
    /// ontem. Continua sendo a melhor âncora disponível (não existe coluna de fim/uso de
    /// sessão), mas o rótulo do card não deve prometer mais que isso.</para>
    /// </summary>
    Task<int> ContarTeleorientacoesHojeAsync(long idClinica, DateTime data);

    /// <summary>REC-10 — primeira escrita de <c>INSERT</c> nesta tabela pelo lado
    /// <c>.NET</c> (até aqui só <c>Update</c>, em <c>AtualizarStatusAsync</c>).</summary>
    Task AddAsync(Agendamento agendamento);

    void Update(Agendamento agendamento);
}
