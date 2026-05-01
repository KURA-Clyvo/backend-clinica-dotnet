namespace Kura.Application.DTOs.Notificacao;

public sealed class NotificacaoCreateDto
{
    public long? IdTutor { get; init; }
    public long? IdVeterinario { get; init; }
    public string DsTitulo { get; init; } = string.Empty;
    public string DsMensagem { get; init; } = string.Empty;
}
