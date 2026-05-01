namespace Kura.Application.DTOs.Exame;

public sealed class ExameCreateDto
{
    public long IdPet { get; init; }
    public long IdVeterinario { get; init; }
    public DateTime DtEvento { get; init; }
    public string DsObservacao { get; init; } = string.Empty;
    public string NmExame { get; init; } = string.Empty;
    public string DsResultado { get; init; } = string.Empty;
    public DateTime DtRealizacao { get; init; }
}
