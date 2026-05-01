namespace Kura.Application.DTOs.Clinica;

public sealed class ClinicaResponseDto
{
    public long Id { get; init; }
    public string NmClinica { get; init; } = string.Empty;
    public string NrCnpj { get; init; } = string.Empty;
    public string DsEndereco { get; init; } = string.Empty;
    public string NrTelefone { get; init; } = string.Empty;
    public string DsEmail { get; init; } = string.Empty;
    public char StAtiva { get; init; }
}
