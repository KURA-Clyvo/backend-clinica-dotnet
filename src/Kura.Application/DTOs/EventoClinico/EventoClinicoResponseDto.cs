namespace Kura.Application.DTOs.EventoClinico;

public sealed class EventoClinicoResponseDto
{
    public long Id { get; init; }
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public long IdTipoEvento { get; init; }
    public string NmTipoEvento { get; init; } = string.Empty;
    public DateTime DtEvento { get; init; }
    public string DsObservacao { get; init; } = string.Empty;
    public char StAtiva { get; init; }
}
