namespace Kura.Application.DTOs.Tutor;

/// <summary>
/// REC-02 (KURA_BACKLOG_RECEPCAO.md): resposta de <c>POST /api/v1/tutores/{id}/convite</c> — o
/// novo invite gerado, mesmo formato de <see cref="TutorComInviteResponseDto.Invite"/> e
/// <see cref="TutorComInviteResponseDto.DsLinkConvite"/> (A-8), sem repetir os campos do tutor
/// (a REC-02 não altera dado de tutor, só reemite o convite).
/// </summary>
public sealed class InviteTutorReemitidoResponseDto
{
    public InviteTutorResponseDto Invite { get; init; } = null!;

    // A-8: null quando Convite:UrlBaseAppTutor não está configurado — mesmo tratamento do app
    // cliente já feito para TutorComInviteResponseDto.DsLinkConvite (REC-01/REC-03).
    public string? DsLinkConvite { get; init; }
}
