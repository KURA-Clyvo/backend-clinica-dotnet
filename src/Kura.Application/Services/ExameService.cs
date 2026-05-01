namespace Kura.Application.Services;

using Kura.Application.DTOs.Exame;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class ExameService : IExameService
{
    private const long IdTipoEventoExame = 3L;

    private readonly IRepository<EventoClinico> _eventoRepository;
    private readonly IRepository<Exame> _exameRepository;
    private readonly IUnitOfWork _uow;

    public ExameService(
        IRepository<EventoClinico> eventoRepository,
        IRepository<Exame> exameRepository,
        IUnitOfWork uow)
    {
        _eventoRepository = eventoRepository;
        _exameRepository = exameRepository;
        _uow = uow;
    }

    public async Task<ExameResponseDto> CreateAsync(ExameCreateDto dto)
    {
        var evento = new EventoClinico
        {
            IdPet = dto.IdPet,
            IdVeterinario = dto.IdVeterinario,
            IdTipoEvento = IdTipoEventoExame,
            DtEvento = dto.DtEvento,
            DsObservacao = dto.DsObservacao
        };

        await _eventoRepository.AddAsync(evento);
        await _uow.CommitAsync();

        var exame = new Exame
        {
            IdEventoClinico = evento.Id,
            NmExame = dto.NmExame,
            DsResultado = dto.DsResultado,
            DtRealizacao = dto.DtRealizacao
        };

        await _exameRepository.AddAsync(exame);
        await _uow.CommitAsync();

        return BuildResponse(evento, exame);
    }

    public async Task<ExameResponseDto> GetByEventoClinicoAsync(long idEvento)
    {
        var exames = await _exameRepository.FindAsync(e => e.IdEventoClinico == idEvento);
        var exame = exames.FirstOrDefault()
            ?? throw new EntidadeNaoEncontradaException("Exame", idEvento);

        var evento = await _eventoRepository.GetByIdAsync(exame.IdEventoClinico)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", exame.IdEventoClinico);

        return BuildResponse(evento, exame);
    }

    public async Task<IEnumerable<ExameResponseDto>> GetByPetAsync(long idPet)
    {
        var eventos = await _eventoRepository.FindAsync(
            e => e.IdPet == idPet && e.IdTipoEvento == IdTipoEventoExame);

        var result = new List<ExameResponseDto>();
        foreach (var evento in eventos)
        {
            var exames = await _exameRepository.FindAsync(e => e.IdEventoClinico == evento.Id);
            var exame = exames.FirstOrDefault();
            if (exame is not null)
            {
                result.Add(BuildResponse(evento, exame));
            }
        }
        return result;
    }

    private static ExameResponseDto BuildResponse(EventoClinico evento, Exame exame) => new()
    {
        IdEventoClinico = evento.Id,
        Id = exame.Id,
        IdPet = evento.IdPet,
        IdVeterinario = evento.IdVeterinario,
        DtEvento = evento.DtEvento,
        DsObservacao = evento.DsObservacao,
        NmExame = exame.NmExame,
        DsResultado = exame.DsResultado,
        DtRealizacao = exame.DtRealizacao,
        StAtiva = exame.StAtiva
    };
}
