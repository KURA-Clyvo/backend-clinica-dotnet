namespace Kura.Application.DTOs.Clinica;

public sealed class ClinicaCreateDto
{
    public string NmClinica { get; init; } = string.Empty;
    public string NrCnpj { get; init; } = string.Empty;
    public string DsEndereco { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
}
