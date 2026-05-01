namespace Kura.Application.DTOs.Vacina;

public sealed class VacinaResponseDto
{
    public long IdEventoClinico { get; init; }
    public long Id { get; init; }
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public DateTime DtEvento { get; init; }
    public string NmVacina { get; init; } = string.Empty;
    public string NrLote { get; init; } = string.Empty;
    public string DsFabricante { get; init; } = string.Empty;
    public DateTime? DtProximaDose { get; init; }
    public char StAtiva { get; init; }
}
