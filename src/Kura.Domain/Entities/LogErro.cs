namespace Kura.Domain.Entities;

public class LogErro
{
    public long IdLog { get; set; }
    public string DsEndpoint { get; set; } = string.Empty;
    public string DsMetodo { get; set; } = string.Empty;
    public string DsMensagem { get; set; } = string.Empty;
    public string? DsStackTrace { get; set; }
    public int NrStatusCode { get; set; }
    public DateTime DtOcorrencia { get; set; } = DateTime.UtcNow;
}
