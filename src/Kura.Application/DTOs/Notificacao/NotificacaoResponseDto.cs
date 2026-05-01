namespace Kura.Application.DTOs.Notificacao;

public sealed class NotificacaoResponseDto
{
    public long Id { get; init; }
    public long? IdTutor { get; init; }
    public long? IdVeterinario { get; init; }
    public string DsTitulo { get; init; } = string.Empty;
    public string DsMensagem { get; init; } = string.Empty;
    public char StLida { get; init; }
    public DateTime? DtLeitura { get; init; }
    public char StAtiva { get; init; }
}
