namespace Kura.Application.DTOs.AlertaTemperatura;

public sealed class AlertaTemperaturaResponseDto
{
    public long Id { get; init; }
    public long IdLeituraTemperatura { get; init; }
    public string DsTipoAlerta { get; init; } = string.Empty;
    public decimal VlLimite { get; init; }
    public string DsMensagem { get; init; } = string.Empty;
    public char StResolvido { get; init; }
}
