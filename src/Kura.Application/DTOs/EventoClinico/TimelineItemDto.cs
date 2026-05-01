namespace Kura.Application.DTOs.EventoClinico;

public sealed class TimelineItemDto
{
    public long IdEventoClinico { get; init; }
    public long IdPet { get; init; }
    public string NmPet { get; init; } = string.Empty;
    public string NmTipo { get; init; } = string.Empty;
    public DateTime DtEvento { get; init; }
    public string DsObservacao { get; init; } = string.Empty;
    public string NmVeterinario { get; init; } = string.Empty;
}
