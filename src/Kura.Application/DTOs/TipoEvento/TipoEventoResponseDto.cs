namespace Kura.Application.DTOs.TipoEvento;

public sealed class TipoEventoResponseDto
{
    public long Id { get; init; }
    public string CdTipo { get; init; } = string.Empty;
    public string NmTipo { get; init; } = string.Empty;
}
