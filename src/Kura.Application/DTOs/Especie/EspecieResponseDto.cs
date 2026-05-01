namespace Kura.Application.DTOs.Especie;

public sealed class EspecieResponseDto
{
    public long Id { get; init; }
    public string NmEspecie { get; init; } = string.Empty;
    public char StAtiva { get; init; }
}
