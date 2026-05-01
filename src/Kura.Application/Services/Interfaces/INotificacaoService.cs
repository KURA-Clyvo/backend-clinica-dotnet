namespace Kura.Application.Services.Interfaces;

using Kura.Application.DTOs.Notificacao;

public interface INotificacaoService
{
    Task<NotificacaoResponseDto> CreateAsync(NotificacaoCreateDto dto);
    Task<IEnumerable<NotificacaoResponseDto>> GetAllByClinicaAsync(long idClinica, bool? apenasNaoLidas);
    Task<IEnumerable<NotificacaoResponseDto>> GetByTutorAsync(long idTutor);
    Task<IEnumerable<NotificacaoResponseDto>> GetByVeterinarioAsync(long idVet);
    Task MarcarLidaAsync(long id);
}
