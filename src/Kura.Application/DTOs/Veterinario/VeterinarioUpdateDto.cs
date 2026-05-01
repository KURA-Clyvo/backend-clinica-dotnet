namespace Kura.Application.DTOs.Veterinario;

public sealed class VeterinarioUpdateDto
{
    public string NmVeterinario { get; init; } = string.Empty;
    public string NrCrmv { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
}
