namespace Kura.Application.Services;

using Kura.Application.DTOs.Prescricao;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class PrescricaoService : IPrescricaoService
{
    private const long IdTipoEventoPrescricao = 2L;

    private readonly IRepository<EventoClinico> _eventoRepository;
    private readonly IRepository<Prescricao> _prescricaoRepository;
    private readonly IUnitOfWork _uow;

    public PrescricaoService(
        IRepository<EventoClinico> eventoRepository,
        IRepository<Prescricao> prescricaoRepository,
        IUnitOfWork uow)
    {
        _eventoRepository = eventoRepository;
        _prescricaoRepository = prescricaoRepository;
        _uow = uow;
    }

    public async Task<PrescricaoResponseDto> CreateAsync(PrescricaoCreateDto dto)
    {
        var evento = new EventoClinico
        {
            IdPet = dto.IdPet,
            IdVeterinario = dto.IdVeterinario,
            IdTipoEvento = IdTipoEventoPrescricao,
            DtEvento = dto.DtEvento,
            DsObservacao = dto.DsObservacao
        };

        await _eventoRepository.AddAsync(evento);
        await _uow.CommitAsync();

        var prescricao = new Prescricao
        {
            IdEventoClinico = evento.Id,
            IdMedicamento = dto.IdMedicamento,
            DsPosologia = dto.DsPosologia,
            NrDuracaoDias = dto.NrDuracaoDias
        };

        await _prescricaoRepository.AddAsync(prescricao);
        await _uow.CommitAsync();

        return BuildResponse(evento, prescricao);
    }

    public async Task<PrescricaoResponseDto> GetByEventoClinicoAsync(long idEvento)
    {
        var prescricoes = await _prescricaoRepository.FindAsync(p => p.IdEventoClinico == idEvento);
        var prescricao = prescricoes.FirstOrDefault()
            ?? throw new EntidadeNaoEncontradaException("Prescricao", idEvento);

        var evento = await _eventoRepository.GetByIdAsync(prescricao.IdEventoClinico)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", prescricao.IdEventoClinico);

        return BuildResponse(evento, prescricao);
    }

    public async Task<IEnumerable<PrescricaoResponseDto>> GetByPetAsync(long idPet)
    {
        var eventos = await _eventoRepository.FindAsync(
            e => e.IdPet == idPet && e.IdTipoEvento == IdTipoEventoPrescricao);

        var result = new List<PrescricaoResponseDto>();
        foreach (var evento in eventos)
        {
            var prescricoes = await _prescricaoRepository.FindAsync(p => p.IdEventoClinico == evento.Id);
            var prescricao = prescricoes.FirstOrDefault();
            if (prescricao is not null)
            {
                result.Add(BuildResponse(evento, prescricao));
            }
        }
        return result;
    }

    private static PrescricaoResponseDto BuildResponse(EventoClinico evento, Prescricao prescricao) => new()
    {
        IdEventoClinico = evento.Id,
        Id = prescricao.Id,
        IdPet = evento.IdPet,
        IdVeterinario = evento.IdVeterinario,
        DtEvento = evento.DtEvento,
        DsObservacao = evento.DsObservacao,
        IdMedicamento = prescricao.IdMedicamento,
        DsPosologia = prescricao.DsPosologia,
        NrDuracaoDias = prescricao.NrDuracaoDias,
        StAtiva = prescricao.StAtiva
    };
}
