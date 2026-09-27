namespace Kura.Domain.Entities;

public class Agendamento
{
    public long Id { get; set; }
    public long IdClinica { get; set; }
    public long? IdPet { get; set; }
    public long? IdTutor { get; set; }
    public long? IdVeterinario { get; set; }
    public string? NmPaciente { get; set; }
    public DateTime DtAgendamento { get; set; }
    public int? NrDuracaoMinutos { get; set; }
    public string? DsServico { get; set; }
    public string? DsTipoConsulta { get; set; }
    public string? StStatus { get; set; }
    public string? DsOrigem { get; set; }
    public bool StAtiva { get; set; } = true;
    public long NrVersion { get; set; }
    // Flyway V5 columns
    public string? DsObservacoes { get; set; }
    public DateTime? DtCriacao { get; set; }
    public DateTime? DtConfirmacao { get; set; }
    public DateTime? DtCancelamento { get; set; }
    public string? DsMotivoCancel { get; set; }
    public long? IdEventoGerado { get; set; }
    // Flyway V10 columns — teleconsulta (Daily.co)
    public string? DsSalaUrl { get; set; }
    public string? DsProvedorVideo { get; set; }
    public bool StTeleconsulta { get; set; }
    public DateTime? DtInicioSessao { get; set; }
    public DateTime? DtFimSessao { get; set; }

    // Flyway V23 columns (backend-tutor-java, REC-07) — recepção. Todas as colunas
    // TIMESTAMP guardam hora LOCAL de America/Sao_Paulo (F-4/A-5), como DtAgendamento.
    /// <summary>Chegada do paciente. Não é ST_STATUS (A-2) — usada para derivar
    /// <c>DsEtapaRecepcao</c> (A-3), nunca lida como transição de estado.</summary>
    public DateTime? DtCheckin { get; set; }
    /// <summary>Início do atendimento. Não é ST_STATUS (A-2) — mesmo raciocínio de
    /// <see cref="DtCheckin"/>.</summary>
    public DateTime? DtInicioAtendimento { get; set; }
    /// <summary>FK opcional para a triagem da Luna que originou este agendamento
    /// (F-3, DsOrigem=TRIAGEM_LUNA). Nula quando a origem é RECEPCAO ou PORTAL.</summary>
    public long? IdTriagemOrigem { get; set; }
    public DateTime? DtLembreteConfirmacao { get; set; }
    /// <summary>SIM | CANCELAR | REMARCAR (A-10) — resposta do tutor ao lembrete D-1.</summary>
    public string? DsRespostaConfirmacao { get; set; }
    public DateTime? DtRespostaConfirmacao { get; set; }

    public Pet? Pet { get; set; }
    public Tutor? Tutor { get; set; }
    public Veterinario? Veterinario { get; set; }
    /// <summary>Navegação opcional para a triagem de origem (ID_TRIAGEM_ORIGEM). FK
    /// opcional ⇒ Include gera LEFT JOIN: se a triagem referenciada pertencer a outra
    /// clínica, o HasQueryFilter de TriagemLuna (KuraDbContext.ApplyTenantFilters) a
    /// exclui e esta propriedade fica null — nunca derruba a linha de Agendamento (A-7,
    /// aceite (b) da REC-09).</summary>
    public TriagemLuna? TriagemOrigem { get; set; }
}
