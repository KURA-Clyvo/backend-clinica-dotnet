namespace Kura.Domain.Entities;

public class Tutor : EntidadeBase
{
    public long IdClinica { get; set; }
    public string NmTutor { get; set; } = string.Empty;
    public string NrCpf { get; set; } = string.Empty;
    public string DsEmail { get; set; } = string.Empty;
    public string NrTelefone { get; set; } = string.Empty;

    // REC-01 (KURA_BACKLOG_RECEPCAO.md, A-12): coluna DS_WHATSAPP existe desde
    // V1__initial_schema.sql:92 do backend-tutor-java (Flyway é a única autoridade de DDL
    // deste ecossistema), mas nunca tinha sido mapeada aqui — nenhum endpoint .NET escrevia
    // nela (o seed-demo-luna.sh gravava por UPDATE SQL bruto). Formato E.164 ("+" + dígitos),
    // ver NormalizadorTelefone.ParaE164.
    public string? DsWhatsapp { get; set; }

    public string StAvisoPrivacidade { get; set; } = "N";
    public DateTime? DtAvisoPrivacidade { get; set; }
    public string? DsVersaoAviso { get; set; }
    public ICollection<TutorPet> TutorPets { get; set; } = [];
    public ICollection<InviteTutor> Invites { get; set; } = [];
}
