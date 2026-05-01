namespace Kura.Application.DTOs.Vacina;

public sealed class VacinaCreateDto
{
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public DateTime DtEvento { get; init; }
    public string DsObservacao { get; init; } = string.Empty;
    public string NmVacina { get; init; } = string.Empty;
    public string NrLote { get; init; } = string.Empty;
    public string DsFabricante { get; init; } = string.Empty;
    public DateTime? DtProximaDose { get; init; }
}
