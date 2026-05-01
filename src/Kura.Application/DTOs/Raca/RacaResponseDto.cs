namespace Kura.Application.DTOs.Raca;

public sealed class RacaResponseDto
{
    public long Id { get; init; }
    public long IdEspecie { get; init; }
    public string NmEspecie { get; init; } = string.Empty;
    public string NmRaca { get; init; } = string.Empty;
    public char StAtiva { get; init; }
}
