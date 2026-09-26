namespace Kura.Application.DTOs.Tutor;

public sealed class TutorComInviteResponseDto
{
    public long Id { get; init; }
    public string NmTutor { get; init; } = string.Empty;
    public string NrCpf { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public bool StAtiva { get; init; }
    public InviteTutorResponseDto Invite { get; init; } = null!;

    // REC-01 (KURA_BACKLOG_RECEPCAO.md, A-8): null quando Convite:UrlBaseAppTutor não está
    // configurado (GeradorLinkConvite) — o app cliente (REC-03) trata como "link não
    // configurado", nunca mostra QR vazio.
    public string? DsLinkConvite { get; init; }
}
