namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Notificacao;

public interface INotificacaoService
{
    Task<NotificacaoResponseDto> CreateAsync(NotificacaoCreateDto dto);
    Task<IEnumerable<NotificacaoResponseDto>> GetByTutorAsync(long idTutor);
    Task<IEnumerable<NotificacaoResponseDto>> GetByVeterinarioAsync(long idVet);
    Task MarcarComoLidaAsync(long id);
}
