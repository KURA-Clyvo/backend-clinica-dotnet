namespace Kura.Application.DTOs.Agenda;

public class AgendaResponseDto
{
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public List<AgendamentoItemDto> Agendamentos { get; set; } = [];
}

public class AgendamentoItemDto
{
    public long IdAgendamento { get; set; }
    public DateTime DtAgendamento { get; set; }
    public int DuracaoMinutos { get; set; }
    public string NmTutor { get; set; } = string.Empty;
    public string NmPet { get; set; } = string.Empty;
    public long IdVeterinario { get; set; }
    public string NmVeterinario { get; set; } = string.Empty;
    public string DsTipoConsulta { get; set; } = string.Empty;
    public string DsStatus { get; set; } = string.Empty;
    public long NrVersion { get; set; }

    // REC-09 (fecha o E21).
    public long? IdPet { get; set; }
    public long? IdTutor { get; set; }
    public DateTime? DtCheckin { get; set; }
    public DateTime? DtInicioAtendimento { get; set; }
    public string? DsOrigem { get; set; }
    /// <summary>Nível de urgência (BAIXA|MEDIA|ALTA) da TRIAGEM_LUNA que originou este
    /// agendamento (ID_TRIAGEM_ORIGEM). Null sem triagem de origem OU quando a triagem
    /// referenciada pertence a outra clínica (A-7 — nunca vaza nível de urgência de
    /// triagem de fora do tenant do JWT).</summary>
    public string? DsNivelUrgenciaOrigem { get; set; }
    public string? DsRespostaConfirmacao { get; set; }
    /// <summary>Etapa de recepção derivada no servidor (A-3), num lugar só —
    /// <c>AgendaService.CalcularEtapaRecepcao</c>. O app só exibe.</summary>
    public string DsEtapaRecepcao { get; set; } = string.Empty;
    /// <summary>URL assinada da variante thumb da foto do pet — mesmo gerador da FT-04
    /// (<c>IGeradorUrlFotoPet</c>), null quando o pet não tem foto.</summary>
    public string? DsFotoThumbUrl { get; set; }
}
