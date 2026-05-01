namespace Kura.Application.DTOs.Prescricao;

public sealed class PrescricaoCreateDto
{
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public DateTime DtEvento { get; init; }
    public string DsObservacao { get; init; } = string.Empty;
    public long IdMedicamento { get; init; }
    public string DsPosologia { get; init; } = string.Empty;
    public int NrDuracaoDias { get; init; }
}
