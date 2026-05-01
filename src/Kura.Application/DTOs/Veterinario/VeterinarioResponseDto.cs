namespace Kura.Application.DTOs.Veterinario;

public sealed class VeterinarioResponseDto
{
    public long Id { get; init; }
    public long IdClinica { get; init; }
    public string NmVeterinario { get; init; } = string.Empty;
    public string NrCrmv { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public char StAtiva { get; init; }
}
