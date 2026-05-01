namespace Kura.Application.Services;

using Kura.Application.DTOs.Vacina;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public sealed class VacinaService : IVacinaService
{
    private const long IdTipoEventoVacina = 1L;

    private readonly IEventoClinicoRepository _eventoRepository;
    private readonly IRepository<Vacina> _vacinaRepository;
    private readonly IUnitOfWork _uow;

    public VacinaService(
        IEventoClinicoRepository eventoRepository,
        IRepository<Vacina> vacinaRepository,
        IUnitOfWork uow)
    {
        _eventoRepository = eventoRepository;
        _vacinaRepository = vacinaRepository;
        _uow = uow;
    }

    public async Task<VacinaResponseDto> CreateAsync(VacinaCreateDto dto)
    {
        var evento = new EventoClinico
        {
            IdPet = dto.IdPet,
            IdVeterinario = dto.IdVeterinario,
            IdTipoEvento = IdTipoEventoVacina,
            DtEvento = dto.DtEvento,
            DsObservacao = dto.DsObservacao
        };

        // Navigation property — EF Core insere EventoClinico primeiro e usa o Id gerado na FK
        var vacina = new Vacina
        {
            EventoClinico = evento,
            NmVacina = dto.NmVacina,
            NrLote = dto.NrLote,
            DsFabricante = dto.DsFabricante,
            DtProximaDose = dto.DtProximaDose
        };

        await _vacinaRepository.AddAsync(vacina);
        await _uow.CommitAsync();

        return BuildResponse(evento, vacina);
    }

    public async Task<VacinaResponseDto> GetByEventoClinicoAsync(long idEvento)
    {
        var vacinas = await _vacinaRepository.FindAsync(v => v.IdEventoClinico == idEvento);
        var vacina = vacinas.FirstOrDefault()
            ?? throw new EntidadeNaoEncontradaException("Vacina", idEvento);

        var evento = await _eventoRepository.GetByIdAsync(vacina.IdEventoClinico)
            ?? throw new EntidadeNaoEncontradaException("EventoClinico", vacina.IdEventoClinico);

        return BuildResponse(evento, vacina);
    }

    public async Task<IEnumerable<VacinaResponseDto>> GetByPetAsync(long idPet)
    {
        var eventos = await _eventoRepository.GetByFiltersAsync(idPet, IdTipoEventoVacina, null, null, null);
        var result = new List<VacinaResponseDto>();
        foreach (var evento in eventos)
        {
            var vacinas = await _vacinaRepository.FindAsync(v => v.IdEventoClinico == evento.Id);
            var vacina = vacinas.FirstOrDefault();
            if (vacina is not null)
                result.Add(BuildResponse(evento, vacina));
        }
        return result;
    }

    public async Task<IEnumerable<VacinaResponseDto>> GetProximasVacinasAsync(long idPet)
    {
        var hoje = DateTime.UtcNow.Date;
        var eventos = await _eventoRepository.GetByFiltersAsync(idPet, IdTipoEventoVacina, null, null, null);
        var result = new List<VacinaResponseDto>();
        foreach (var evento in eventos)
        {
            var vacinas = await _vacinaRepository.FindAsync(
                v => v.IdEventoClinico == evento.Id && v.DtProximaDose.HasValue && v.DtProximaDose.Value >= hoje);
            var vacina = vacinas.FirstOrDefault();
            if (vacina is not null)
                result.Add(BuildResponse(evento, vacina));
        }
        return result;
    }

    private static VacinaResponseDto BuildResponse(EventoClinico evento, Vacina vacina) => new()
    {
        IdEventoClinico = evento.Id,
        Id = vacina.Id,
        IdPet = evento.IdPet,
        IdVeterinario = evento.IdVeterinario,
        DtEvento = evento.DtEvento,
        NmVacina = vacina.NmVacina,
        NrLote = vacina.NrLote,
        DsFabricante = vacina.DsFabricante,
        DtProximaDose = vacina.DtProximaDose,
        StAtiva = vacina.StAtiva
    };
}
