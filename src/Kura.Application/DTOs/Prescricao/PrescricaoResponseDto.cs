namespace Kura.Application.DTOs.Prescricao;

public sealed class PrescricaoResponseDto
{
    public long IdEventoClinico { get; init; }
    public long Id { get; init; }
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public DateTime DtEvento { get; init; }
    public string DsObservacao { get; init; } = string.Empty;
    public long IdMedicamento { get; init; }
    public string DsPosologia { get; init; } = string.Empty;
    public int NrDuracaoDias { get; init; }
    public char StAtiva { get; init; }
}
