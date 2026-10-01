namespace Kura.Domain.Entities;

public class Consentimento
{
    public long Id { get; set; }
    public long IdTutor { get; set; }
    public string DsTipo { get; set; } = string.Empty;
    public char StAceito { get; set; }
    public string NrVersaoTermo { get; set; } = string.Empty;
    public DateTime DtConsentimento { get; set; }

    // REC-15: DT_REVOGACAO existe na tabela desde V1__initial_schema.sql
    // (backend-tutor-java) mas não estava mapeada aqui até esta task — nenhum
    // consumidor .NET anterior precisou dela (TeleconsultaService.GarantirConsentimentoAsync
    // checa só StAceito, ver achado registrado no diário da REC-15). REC-15 precisa dela
    // para reproduzir fielmente a mesma regra de ST_CONSENTE_LEMBRETE que
    // VW_VACINAS_VENCENDO (backend-tutor-java, V21) calcula: aceito E não revogado.
    public DateTime? DtRevogacao { get; set; }
}
