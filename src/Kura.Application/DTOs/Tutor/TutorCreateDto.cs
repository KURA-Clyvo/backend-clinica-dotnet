namespace Kura.Application.DTOs.Tutor;

public sealed class TutorCreateDto
{
    public string NmTutor { get; init; } = string.Empty;
    public string NrCpf { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;

    // REC-01 (KURA_BACKLOG_RECEPCAO.md, A-9/A-12): campos novos da recepção.
    // DsWhatsapp opcional — ausente/vazio ⇒ o serviço usa o mesmo número de NrTelefone
    // normalizado ("mesmo número", G0 item 4). StAvisoPrivacidadeInformado é obrigatório
    // (validado por TutorCreateValidator): false/ausente ⇒ 400, nenhuma linha gravada.
    public string? DsWhatsapp { get; init; }
    public bool StAvisoPrivacidadeInformado { get; init; }

    public string DsCanalConvite { get; init; } = "WHATSAPP";
}
