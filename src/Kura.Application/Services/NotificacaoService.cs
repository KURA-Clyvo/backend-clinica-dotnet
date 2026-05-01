namespace Kura.Application.Services;

using Kura.Application.DTOs.Notificacao;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class NotificacaoService : INotificacaoService
{
    private readonly IRepository<Notificacao> _repository;
    private readonly IUnitOfWork _uow;

    public NotificacaoService(IRepository<Notificacao> repository, IUnitOfWork uow)
    {
        _repository = repository;
        _uow = uow;
    }

    public async Task<NotificacaoResponseDto> CreateAsync(NotificacaoCreateDto dto)
    {
        var notificacao = new Notificacao
        {
            IdTutor = dto.IdTutor,
            IdVeterinario = dto.IdVeterinario,
            DsTitulo = dto.DsTitulo,
            DsMensagem = dto.DsMensagem
        };
        await _repository.AddAsync(notificacao);
        await _uow.CommitAsync();
        return ToResponse(notificacao);
    }

    public async Task<IEnumerable<NotificacaoResponseDto>> GetByTutorAsync(long idTutor)
    {
        var notificacoes = await _repository.FindAsync(n => n.IdTutor == idTutor);
        return notificacoes.Select(ToResponse);
    }

    public async Task<IEnumerable<NotificacaoResponseDto>> GetByVeterinarioAsync(long idVet)
    {
        var notificacoes = await _repository.FindAsync(n => n.IdVeterinario == idVet);
        return notificacoes.Select(ToResponse);
    }

    public async Task MarcarComoLidaAsync(long id)
    {
        var notificacao = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Notificacao", id);

        notificacao.StLida = 'S';
        notificacao.DtLeitura = DateTime.UtcNow;
        notificacao.DtAtualizacao = DateTime.UtcNow;

        _repository.Update(notificacao);
        await _uow.CommitAsync();
    }

    private static NotificacaoResponseDto ToResponse(Notificacao n) => new()
    {
        Id = n.Id,
        IdTutor = n.IdTutor,
        IdVeterinario = n.IdVeterinario,
        DsTitulo = n.DsTitulo,
        DsMensagem = n.DsMensagem,
        StLida = n.StLida,
        DtLeitura = n.DtLeitura,
        StAtiva = n.StAtiva
    };
}
