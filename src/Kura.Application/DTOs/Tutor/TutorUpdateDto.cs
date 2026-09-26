namespace Kura.Application.DTOs.Tutor;

public sealed class TutorUpdateDto
{
    public string NmTutor { get; init; } = string.Empty;
    public string NrCpf { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;

    // G2 fix wave (KURA_BACKLOG_RECEPCAO.md, REC-01, achado Important #1 — LGPD): opcional.
    // Ausente e o WhatsApp atual era "o mesmo número" do telefone antigo ⇒ TutorService.
    // UpdateAsync o acompanha automaticamente quando o telefone muda; ausente e era diferente
    // ⇒ mantém intocado; presente ⇒ normaliza e grava o valor informado. Ver TutorService.
    public string? DsWhatsapp { get; init; }
}
