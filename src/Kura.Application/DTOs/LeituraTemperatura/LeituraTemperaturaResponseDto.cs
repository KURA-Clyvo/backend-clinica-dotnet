namespace Kura.Application.DTOs.LeituraTemperatura;

public sealed class LeituraTemperaturaResponseDto
{
    public long Id { get; init; }
    public long IdDispositivoIot { get; init; }
    public decimal VlTemperatura { get; init; }
    public decimal? VlUmidade { get; init; }
    public DateTime DtLeitura { get; init; }
}
