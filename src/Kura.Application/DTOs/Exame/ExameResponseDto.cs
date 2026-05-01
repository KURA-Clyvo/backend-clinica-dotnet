namespace Kura.Application.DTOs.Exame;

public sealed class ExameResponseDto
{
    public long IdEventoClinico { get; init; }
    public long Id { get; init; }
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public DateTime DtEvento { get; init; }
    public string DsObservacao { get; init; } = string.Empty;
    public string NmExame { get; init; } = string.Empty;
    public string DsResultado { get; init; } = string.Empty;
    public DateTime DtRealizacao { get; init; }
    public char StAtiva { get; init; }
}
