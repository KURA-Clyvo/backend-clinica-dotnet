namespace Kura.Application.DTOs.DispositivoIot;

public sealed class DispositivoIotCreateDto
{
    public long IdClinica { get; init; }
    public string CdDispositivo { get; init; } = string.Empty;
    public string DsDescricao { get; init; } = string.Empty;
    public string DsLocalizacao { get; init; } = string.Empty;
}
