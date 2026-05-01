namespace Kura.Application.DTOs.Medicamento;

public sealed class MedicamentoCreateDto
{
    public string NmMedicamento { get; init; } = string.Empty;
    public string DsPrincipioAtivo { get; init; } = string.Empty;
    public string DsApresentacao { get; init; } = string.Empty;
}
