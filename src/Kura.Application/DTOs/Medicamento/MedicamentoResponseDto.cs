namespace Kura.Application.DTOs.Medicamento;

public sealed class MedicamentoResponseDto
{
    public long Id { get; init; }
    public string NmMedicamento { get; init; } = string.Empty;
    public string DsPrincipioAtivo { get; init; } = string.Empty;
    public string DsApresentacao { get; init; } = string.Empty;
    public char StAtiva { get; init; }
}
