namespace Kura.Application.Services;

using Kura.Application.DTOs.Luna;
using Kura.Application.DTOs.Pet;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.Storage;
using Kura.Domain.Tutores;

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
    private readonly IGeradorLinkConvite _geradorLinkConvite;

    public TutorService(
        ITutorRepository repository,
        ITutorPetRepository tutorPetRepository,
        IRepository<Especie> especieRepository,
        IRepository<Raca> racaRepository,
        IInviteTutorRepository inviteRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext,
        IGeradorUrlFotoPet geradorUrlFotoPet,
        IGeradorLinkConvite geradorLinkConvite)
    {
        _repository = repository;
        _tutorPetRepository = tutorPetRepository;
        _especieRepository = especieRepository;
        _racaRepository = racaRepository;
        _inviteRepository = inviteRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
        _geradorUrlFotoPet = geradorUrlFotoPet;
        _geradorLinkConvite = geradorLinkConvite;
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
        // REC-01 (KURA_BACKLOG_RECEPCAO.md, A-12): NrTelefone passou a ser obrigatório e
        // validado por TutorCreateValidator (mesmo NormalizadorTelefone) — o sentinela "Não
        // informado" da TASK-60 deixou de ser produzido por esta rota. Normalizamos de novo
        // aqui (idempotente, ver NormalizadorTelefone) porque é este método — não o validator —
        // quem decide o valor persistido; o throw abaixo é defesa em profundidade, inalcançável
        // quando a requisição passou pelo pipeline de validação normal.
        if (!NormalizadorTelefone.TentarNormalizar(dto.NrTelefone, out var telefoneArmazenado))
            throw new RegraDeNegocioException("Telefone inválido.");

        // A-9: DsWhatsapp ausente/vazio ⇒ "mesmo número" do telefone já normalizado (G0 item 4).
        string whatsappArmazenado;
        if (string.IsNullOrWhiteSpace(dto.DsWhatsapp))
        {
            whatsappArmazenado = telefoneArmazenado;
        }
        else if (!NormalizadorTelefone.TentarNormalizar(dto.DsWhatsapp, out whatsappArmazenado))
        {
            throw new RegraDeNegocioException("WhatsApp inválido.");
        }

        var tutor = new Tutor
        {
            IdClinica = clinicaId,
            NmTutor = dto.NmTutor,
            NrCpf = dto.NrCpf,
            DsEmail = dto.DsEmail,
            NrTelefone = telefoneArmazenado,
            DsWhatsapp = NormalizadorTelefone.ParaE164(whatsappArmazenado),
            // A-9: StAvisoPrivacidade agora DEPENDE do que a recepção de fato confirmou —
            // TutorCreateValidator já exige StAvisoPrivacidadeInformado == true antes de
            // chegar aqui (400 em caso contrário, nenhuma linha gravada), então este ramo
            // "N" é defesa em profundidade, não caminho esperado em produção.
            StAvisoPrivacidade = dto.StAvisoPrivacidadeInformado ? "S" : "N",
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

        // A-8: idClinica vem do parâmetro `clinicaId` (JWT de quem está criando o tutor, ver
        // TutoresController.Create) — NUNCA de um campo do corpo, que este DTO nem declara.
        var link = _geradorLinkConvite.GerarLink(invite.NrToken, clinicaId);
        return ToComInviteResponse(tutor, invite, link);
    }

    public async Task<TutorResponseDto> UpdateAsync(long id, TutorUpdateDto dto)
    {
        var tutor = await _repository.GetByIdAsync(id)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);

        tutor.NmTutor = dto.NmTutor;
        tutor.NrCpf = dto.NrCpf;
        tutor.DsEmail = dto.DsEmail;

        // REC-01 (G0 item 4): o PUT NÃO exige telefone (recomendação do maestro — não quebrar
        // edição parcial de tutor antigo sem telefone); TASK-60 continua coalescendo para o
        // sentinela quando vazio. Quando o campo VEM preenchido, normaliza (idempotente) para o
        // tutor editado casar com o formato que a Luna usa na busca — TutorUpdateValidator já
        // bloqueia formato inválido (400) antes de chegar aqui; o throw é defesa em
        // profundidade, nunca grava lixo mesmo se alcançado por outro caminho.
        if (string.IsNullOrWhiteSpace(dto.NrTelefone))
        {
            tutor.NrTelefone = "Não informado";
        }
        else if (NormalizadorTelefone.TentarNormalizar(dto.NrTelefone, out var telefoneArmazenado))
        {
            tutor.NrTelefone = telefoneArmazenado;
        }
        else
        {
            throw new RegraDeNegocioException("Telefone inválido.");
        }

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
        //
        // REC-01 (G0 item 4): normaliza a ENTRADA antes de buscar, para número nacional e
        // "55…" acharem o mesmo tutor (a busca do repositório é igualdade EXATA sobre
        // DS_TELEFONE, que agora é sempre gravado normalizado). Formato não reconhecido ⇒
        // busca pelo valor cru mesmo (fallback inofensivo: não vai casar com nada gravado
        // depois desta task, então devolve "não encontrado" como já fazia antes de existir
        // normalização nenhuma — nunca lança 400/422 para quem só está consultando).
        var chaveBusca = NormalizadorTelefone.TentarNormalizar(numero, out var normalizado)
            ? normalizado
            : numero;
        var tutor = await _repository.GetByTelefoneAsync(chaveBusca);
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

    private static TutorComInviteResponseDto ToComInviteResponse(Tutor t, InviteTutor i, string? dsLinkConvite) => new()
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
        },
        DsLinkConvite = dsLinkConvite
    };
}
