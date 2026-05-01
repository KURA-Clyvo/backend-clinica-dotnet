namespace Kura.Application.DTOs.EventoClinico;

public sealed class EventoClinicoCreateDto
{
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public long IdTipoEvento { get; init; }
    public DateTime DtEvento { get; init; }
    public string DsObservacao { get; init; } = string.Empty;
}
