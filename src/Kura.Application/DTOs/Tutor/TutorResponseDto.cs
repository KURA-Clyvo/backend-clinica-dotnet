namespace Kura.Application.DTOs.Tutor;

public sealed class TutorResponseDto
{
    public long Id { get; init; }
    public string NmTutor { get; init; } = string.Empty;
    public string NrCpf { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public char StAtiva { get; init; }
}
