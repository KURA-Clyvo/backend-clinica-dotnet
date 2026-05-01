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
    private readonly IClinicaContext _clinicaContext;

    public NotificacaoService(
        IRepository<Notificacao> repository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext)
    {
        _repository = repository;
        _uow = uow;
        _clinicaContext = clinicaContext;
    }

    public async Task<NotificacaoResponseDto> CreateAsync(NotificacaoCreateDto dto)
    {
        var notificacao = new Notificacao
        {
            IdClinica = _clinicaContext.IdClinica,
            IdTutor = dto.IdTutor,
            IdVeterinario = dto.IdVeterinario,
            DsTitulo = dto.DsTitulo,
            DsMensagem = dto.DsMensagem
        };
        await _repository.AddAsync(notificacao);
        await _uow.CommitAsync();
        return ToResponse(notificacao);
    }

    public async Task<IEnumerable<NotificacaoResponseDto>> GetAllByClinicaAsync(long idClinica, bool? apenasNaoLidas)
    {
        var notificacoes = await _repository.FindAsync(n => n.IdClinica == idClinica);
        if (apenasNaoLidas == true)
            notificacoes = notificacoes.Where(n => n.StLida == 'N');
        return notificacoes.Select(ToResponse);
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

    public async Task MarcarLidaAsync(long id)
    {
        var notificacao = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Notificacao", id);

        if (notificacao.IdClinica != _clinicaContext.IdClinica)
            throw new EntidadeNaoEncontradaException("Notificacao", id);

        if (notificacao.StLida == 'S')
            throw new RegraDeNegocioException("Notificação já foi lida.");

        notificacao.StLida = 'S';
        notificacao.DtLeitura = DateTime.UtcNow;
        _repository.Update(notificacao);
        await _uow.CommitAsync();
    }

    private static NotificacaoResponseDto ToResponse(Notificacao n) => new()
    {
        Id = n.Id,
        IdClinica = n.IdClinica,
        IdTutor = n.IdTutor,
        IdVeterinario = n.IdVeterinario,
        DsTitulo = n.DsTitulo,
        DsMensagem = n.DsMensagem,
        StLida = n.StLida,
        DtLeitura = n.DtLeitura,
        DtCriacao = n.DtCriacao,
        StAtiva = n.StAtiva
    };
}
