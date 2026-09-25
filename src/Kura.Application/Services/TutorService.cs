namespace Kura.Application.Services;

using Kura.Application.DTOs.Luna;
using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.Storage;

public sealed class TutorService : ITutorService
{
    private readonly ITutorRepository _repository;
    private readonly ITutorPetRepository _tutorPetRepository;
    private readonly IRepository<Especie> _especieRepository;
    private readonly IRepository<Raca> _racaRepository;
    private readonly IInviteTutorRepository _inviteRepository;
    private readonly IUnitOfWork _uow;
    private readonly IClinicaContext _clinicaContext;
    private readonly IGeradorUrlFotoPet _geradorUrlFotoPet;

    public TutorService(
        ITutorRepository repository,
        ITutorPetRepository tutorPetRepository,
        IRepository<Especie> especieRepository,
        IRepository<Raca> racaRepository,
        IInviteTutorRepository inviteRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext,
        IGeradorUrlFotoPet geradorUrlFotoPet)
    {
        _repository = repository;
        _tutorPetRepository = tutorPetRepository;
        _especieRepository = especieRepository;
        _racaRepository = racaRepository;
        _inviteRepository = inviteRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
        _geradorUrlFotoPet = geradorUrlFotoPet;
    }

    public async Task<IEnumerable<TutorResponseDto>> SearchAsync(string? busca)
    {
        // TASK-21: idClinica passado explicitamente — defesa em profundidade além do
        // HasQueryFilter global do KuraDbContext (mesmo padrão de AgendaService/PetService).
        var tutores = await _repository.SearchAsync(busca, _clinicaContext.IdClinica);
        return tutores.Select(ToResponse);
    }

    public async Task<TutorResponseDto> GetByIdAsync(long id)
    {
        var tutor = await _repository.GetByIdAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);
        return ToResponse(tutor);
    }

    public async Task<IEnumerable<PetResponseDto>> GetPetsAsync(long id)
    {
        _ = await _repository.GetByIdAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);

        var vinculos = await _tutorPetRepository.GetByTutorIdAsync(id);
        var result = new List<PetResponseDto>();
        foreach (var vinculo in vinculos)
        {
            var pet = vinculo.Pet;
            var especie = await _especieRepository.GetByIdAsync(pet.IdEspecie);
            var raca = await _racaRepository.GetByIdAsync(pet.IdRaca);
            result.Add(new PetResponseDto
            {
                Id = pet.Id,
                NmPet = pet.NmPet,
                IdEspecie = pet.IdEspecie,
                NmEspecie = especie?.NmEspecie ?? string.Empty,
                IdRaca = pet.IdRaca,
                NmRaca = raca?.NmRaca ?? string.Empty,
                IdVeterinarioResp = pet.IdVeterinarioResp,
                DtNascimento = pet.DtNascimento,
                SgSexo = pet.SgSexo,
                SgPorte = pet.SgPorte,
                StAtiva = pet.StAtiva,
                // FT-04 (backlog KURA_BACKLOG_FOTO_PET.md): esta lista usa o MESMO
                // PetResponseDto de PetService.BuildResponseAsync — medido, é o único outro
                // construtor de PetResponseDto no projeto (GET /api/v1/tutores/{id}/pets).
                DsFotoUrl = _geradorUrlFotoPet.GerarUrl(pet.DsFotoChave, ChaveFotoPet.SufixoMedia),
                DsFotoThumbUrl = _geradorUrlFotoPet.GerarUrl(pet.DsFotoChave, ChaveFotoPet.SufixoThumb),
            });
        }
        return result;
    }

    public async Task<TutorComInviteResponseDto> CreateAsync(TutorCreateDto dto, long clinicaId)
    {
        var tutor = new Tutor
        {
            IdClinica = clinicaId,
            NmTutor = dto.NmTutor,
            NrCpf = dto.NrCpf,
            DsEmail = dto.DsEmail,
            // TASK-60: TUTOR.DS_TELEFONE é NOT NULL (V1:91, migration imutável) e o Oracle trata
            // VARCHAR2 vazio como NULL. TutorCreateValidator só valida NmTutor/NrCpf/DsEmail,
            // nunca teve regra NotEmpty() para NrTelefone — sem este coalesce, um payload sem
            // esse campo estoura ORA-01400 (500) no INSERT. Mesmo padrão da TASK-56: a restrição
            // de armazenamento se resolve aqui, não como regra de negócio no validator.
            NrTelefone = string.IsNullOrWhiteSpace(dto.NrTelefone)
                ? "Não informado"
                : dto.NrTelefone,
            StAvisoPrivacidade = "S",
            DtAvisoPrivacidade = DateTime.UtcNow,
            DsVersaoAviso = "v1.0"
        };
        await _repository.AddAsync(tutor);

        var invite = new InviteTutor
        {
            Tutor = tutor,
            NrToken = Guid.NewGuid(),
            DtExpiracao = tutor.DtCriacao.AddDays(7),
            DsCanal = dto.DsCanalConvite,
        };
        await _inviteRepository.AddAsync(invite);

        await _uow.CommitAsync();
        return ToComInviteResponse(tutor, invite);
    }

    public async Task<TutorResponseDto> UpdateAsync(long id, TutorUpdateDto dto)
    {
        var tutor = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);

        tutor.NmTutor = dto.NmTutor;
        tutor.NrCpf = dto.NrCpf;
        tutor.DsEmail = dto.DsEmail;
        // TASK-60: mesmo coalesce de CreateAsync — TutorUpdateValidator também nunca teve regra
        // NotEmpty() para NrTelefone.
        tutor.NrTelefone = string.IsNullOrWhiteSpace(dto.NrTelefone)
            ? "Não informado"
            : dto.NrTelefone;

        _repository.Update(tutor);
        await _uow.CommitAsync();
        return ToResponse(tutor);
    }

    public async Task SoftDeleteAsync(long id)
    {
        var tutor = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);
        _repository.SoftDelete(tutor);
        await _uow.CommitAsync();
    }

    public async Task<TutorContextoLunaDto?> BuscarContextoPorTelefoneAsync(string numero)
    {
        // TASK-67: SEM idClinica — este é justamente o endpoint que resolve a clínica a
        // partir do telefone para um caller sem JWT (a IA Luna). Ver comentário em
        // ITutorRepository.GetByTelefoneAsync. Mensagem de erro (se o tutor não existir)
        // nunca deve interpolar `numero` — LGPD, ver LgpdNaoVazamentoTests.
        // TASK-79: `tutor is null` cobre TANTO "nenhum tutor com esse telefone" QUANTO
        // "mais de um tutor ativo com esse telefone, qualquer clínica — inclusive dois
        // da MESMA clínica" — o repositório já resolve a ambiguidade para null, ver
        // TutorRepository.
        var tutor = await _repository.GetByTelefoneAsync(numero);
        if (tutor is null)
            return null;

        var vinculos = await _tutorPetRepository.GetByTutorIdAsync(tutor.Id);
        var pets = new List<PetResumoLunaDto>();
        foreach (var vinculo in vinculos)
        {
            var pet = vinculo.Pet;
            var especie = await _especieRepository.GetByIdAsync(pet.IdEspecie);
            var raca = await _racaRepository.GetByIdAsync(pet.IdRaca);
            pets.Add(new PetResumoLunaDto
            {
                IdPet = pet.Id,
                NmPet = pet.NmPet,
                NmEspecie = especie?.NmEspecie ?? string.Empty,
                NmRaca = raca?.NmRaca
            });
        }

        return new TutorContextoLunaDto
        {
            IdTutor = tutor.Id,
            NmTutor = tutor.NmTutor,
            DsWhatsapp = tutor.NrTelefone,
            IdClinica = tutor.IdClinica,
            Pets = pets
        };
    }

    private static TutorResponseDto ToResponse(Tutor t) => new()
    {
        Id = t.Id,
        NmTutor = t.NmTutor,
        NrCpf = t.NrCpf,
        DsEmail = t.DsEmail,
        NrTelefone = t.NrTelefone,
        StAtiva = t.StAtiva
    };

    private static TutorComInviteResponseDto ToComInviteResponse(Tutor t, InviteTutor i) => new()
    {
        Id = t.Id,
        NmTutor = t.NmTutor,
        NrCpf = t.NrCpf,
        DsEmail = t.DsEmail,
        NrTelefone = t.NrTelefone,
        StAtiva = t.StAtiva,
        Invite = new InviteTutorResponseDto
        {
            Id = i.Id,
            NrToken = i.NrToken,
            DtExpiracao = i.DtExpiracao,
            DsCanal = i.DsCanal,
            StUtilizado = i.StUtilizado
        }
    };
}
