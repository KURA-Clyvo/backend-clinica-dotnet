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
    private readonly IContaTutorRepository _contaTutorRepository;
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
        IContaTutorRepository contaTutorRepository,
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
        _contaTutorRepository = contaTutorRepository;
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

        // R1a (G2b fix wave 2, achado Important — LGPD): capturado ANTES de mutar
        // tutor.NrTelefone, porque a regra de "acompanhar o telefone novo" olha o estado
        // ANTIGO. CORREÇÃO da fix wave 1: aquela versão comparava `DsWhatsapp` com
        // `'+' + tutor.NrTelefone` CRU, assumindo "o valor armazenado já é o que
        // TentarNormalizar produziria" — FALSO para tutor legado pré-REC-01, cujo
        // `DS_TELEFONE` pode estar em formato nacional cru (ex.: `seed-demo-luna.sh` antes do
        // fix A1 de 16/09 gravava `TELEFONE_CONTATO` nacional e `DS_WHATSAPP` em E.164 do MESMO
        // número por `UPDATE` SQL direto) — nesse caso "+11988880001" ≠ "+5511988880001" e a
        // comparação crua nunca reconhecia "mesmo número". Fix: normaliza o telefone ANTIGO
        // antes de comparar. Se ele não for normalizável (sentinela "Não informado", lixo
        // legado) ⇒ trata como "NÃO era o mesmo número" — EXCETO quando `DsWhatsapp` já é
        // `null` (nada para comparar; é o estado de todo tutor criado antes da REC-01, G0 item
        // 4: nulo nos 18 tutores existentes), que sempre acompanha.
        bool whatsappEraMesmoNumero;
        if (tutor.DsWhatsapp is null)
        {
            whatsappEraMesmoNumero = true;
        }
        else if (NormalizadorTelefone.TentarNormalizar(tutor.NrTelefone, out var telefoneAntigoNormalizado))
        {
            whatsappEraMesmoNumero = tutor.DsWhatsapp == NormalizadorTelefone.ParaE164(telefoneAntigoNormalizado);
        }
        else
        {
            whatsappEraMesmoNumero = false;
        }

        // R1c (G2b fix wave 2, achado Important — LGPD, mesmo defeito por outro vetor): a fix
        // wave 1 gravava o sentinela "Não informado" por cima do telefone real quando o PUT
        // vinha sem telefone, e isso desligava o acompanhamento do WhatsApp PARA SEMPRE (o PUT
        // seguinte com telefone novo comparava contra "+Não informado", nunca normalizável ⇒
        // ramo 3 ⇒ WhatsApp nunca mais acompanha — reproduz o mesmo sintoma do M5 da G2
        // original, por um vetor diferente). Ruling do maestro: PUT sem telefone (ausente/
        // vazio) MANTÉM o telefone atual — nunca sobrescreve. O sentinela "Não informado" deixa
        // de ser PRODUZIDO por qualquer código novo (Create já o exige; Update agora preserva);
        // ele só existe em dado legado que nenhuma rota atual volta a gravar.
        var telefoneMudou = !string.IsNullOrWhiteSpace(dto.NrTelefone);
        if (telefoneMudou)
        {
            if (NormalizadorTelefone.TentarNormalizar(dto.NrTelefone, out var telefoneArmazenado))
            {
                tutor.NrTelefone = telefoneArmazenado;
            }
            else
            {
                throw new RegraDeNegocioException("Telefone inválido.");
            }
        }
        // else: ausente/vazio ⇒ mantém tutor.NrTelefone como estava — TutorUpdateValidator não
        // exige o campo (recomendação do maestro, G0 item 4 — não quebrar edição parcial).

        // 3 ramos, na ordem do achado original (M5/M6 da G2 — PUT trocava DS_TELEFONE e deixava
        // DS_WHATSAPP com o número antigo; o lembrete de vacina ia para o número errado):
        // 1) DsWhatsapp veio no corpo (não vazio/whitespace — "" conta como AUSENTE, não como
        //    "limpar o campo": não há hoje forma de limpar o WhatsApp pelo PUT, documentado em
        //    TutorUpdateDto) ⇒ normaliza e grava (a recepção está corrigindo os dois campos
        //    explicitamente).
        // 2) Não veio, o TELEFONE de fato mudou (R1c: se não mudou, não há o que "acompanhar")
        //    E o WhatsApp ERA "o mesmo número" do telefone ANTIGO (R1a) ⇒ acompanha o telefone
        //    NOVO (já normalizado e válido por construção, dado que telefoneMudou implica que o
        //    bloco acima já validou/normalizou com sucesso).
        // 3) Qualquer outro caso ⇒ mantém intocado (era intencionalmente um número de WhatsApp
        //    distinto do telefone de contato, ou o telefone não mudou de verdade).
        if (!string.IsNullOrWhiteSpace(dto.DsWhatsapp))
        {
            if (!NormalizadorTelefone.TentarNormalizar(dto.DsWhatsapp, out var whatsappArmazenado))
                throw new RegraDeNegocioException("WhatsApp inválido.");
            tutor.DsWhatsapp = NormalizadorTelefone.ParaE164(whatsappArmazenado);
        }
        else if (telefoneMudou && whatsappEraMesmoNumero)
        {
            tutor.DsWhatsapp = NormalizadorTelefone.ParaE164(tutor.NrTelefone);
        }
        // senão: mantém tutor.DsWhatsapp como estava.

        _repository.Update(tutor);
        await _uow.CommitAsync();
        return ToResponse(tutor);
    }

    public async Task<InviteTutorReemitidoResponseDto> ReemitirConviteAsync(long id, long clinicaId)
    {
        // A-7: escopo por clínica À MÃO, mesmo método já usado por GetByIdAsync/UpdateAsync —
        // GetByIdAsync(id, idClinica) devolve null tanto para tutor inexistente quanto para
        // tutor de OUTRA clínica (sem oráculo de existência: os dois casos são o MESMO 404), e
        // o HasQueryFilter global de Tutor (StAtiva && tenant) já exclui tutor inativo da mesma
        // consulta — três motivos de "não encontrado" convergindo no mesmo caminho, de
        // propósito (Agendamento está na allowlist do filtro de tenant, não Tutor — Tutor JÁ
        // está no filtro global desde a TASK-21).
        var tutor = await _repository.GetByIdAsync(id, clinicaId)
            ?? throw new EntidadeNaoEncontradaException("Tutor", id);

        // Tutor que já concluiu o onboarding (CONTA_TUTOR é do Java, só leitura aqui —
        // ReadOnlyTablesInterceptor) não tem por que reemitir convite.
        if (await _contaTutorRepository.ExisteContaAsync(tutor.Id))
            throw new TutorComContaExistenteException(tutor.Id);

        // Cancela (soft delete) todo invite ainda ATIVO e NÃO UTILIZADO do tutor. O
        // HasQueryFilter de InviteTutorConfiguration (e => e.StAtiva) já restringe este
        // FindAsync aos convites ainda visíveis — um invite já cancelado por uma reemissão
        // anterior não aparece aqui de novo. SoftDelete só marca StAtiva=false no
        // ChangeTracker (Repository.cs:45-50); nada é persistido até o CommitAsync único
        // abaixo, que também grava o invite novo — se o insert falhar, o SaveChanges inteiro
        // falha e os SoftDelete não persistem (mesma transação implícita do EF Core).
        var invitesAtivos = await _inviteRepository.FindAsync(
            i => i.IdTutor == tutor.Id && !i.StUtilizado);
        foreach (var antigo in invitesAtivos)
            _inviteRepository.SoftDelete(antigo);

        var novoInvite = new InviteTutor
        {
            IdTutor = tutor.Id,
            Tutor = tutor,
            NrToken = Guid.NewGuid(),
            DtExpiracao = DateTime.UtcNow.AddDays(7),
            // Mesmo canal default da entidade (DsCanal = "WHATSAPP") — REC-02 não recebe body,
            // então não há canal explícito para propagar.
        };
        await _inviteRepository.AddAsync(novoInvite);

        await _uow.CommitAsync();

        // A-8: idClinica é o PARÂMETRO (JWT de quem está reemitindo, ver TutoresController),
        // nunca um valor do corpo — mesma regra da CreateAsync (mordida (c) da REC-01).
        var link = _geradorLinkConvite.GerarLink(novoInvite.NrToken, clinicaId);

        return new InviteTutorReemitidoResponseDto
        {
            Invite = new InviteTutorResponseDto
            {
                Id = novoInvite.Id,
                NrToken = novoInvite.NrToken,
                DtExpiracao = novoInvite.DtExpiracao,
                DsCanal = novoInvite.DsCanal,
                StUtilizado = novoInvite.StUtilizado
            },
            DsLinkConvite = link
        };
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
        // TASK-79: "não encontrado" cobre TANTO "nenhum tutor com esse telefone" QUANTO
        // "mais de um tutor ativo com esse telefone, qualquer clínica — inclusive dois
        // da MESMA clínica".
        //
        // I3 (G2 fix wave 1, achado Important #3): a Luna SEMPRE manda os dígitos
        // internacionais completos (twilio_inbound.py:32 só tira "whatsapp:"/"+", nunca
        // reformata) — tenta PRIMEIRO com a entrada exatamente como chegou (só dígitos, sem
        // reinterpretar DDI). Normalizar a entrada ANTES de buscar (versão anterior desta
        // task) reescrevia um estrangeiro de 10/11 dígitos sem DDI Brasil (ex.: EUA/Canadá,
        // "14155550100") para "5514155550100", que NUNCA casa com o valor gravado no cadastro
        // (ramo `+` explícito grava "14155550100", sem prefixo — ver NormalizadorTelefone) —
        // ou seja, o caso 6 do G0 nunca era encontrado pela Luna, ao contrário do que a tabela
        // original alegava. Só tenta com o prefixo "55" se a primeira busca não achar E a
        // entrada tiver 10/11 dígitos (formato nacional sem DDI — nunca é o que a Luna manda,
        // mas cobre chamada manual/smoke com número legado).
        //
        // R3a (G2b fix wave 2, achado Minor): TASK-79 precisa valer sobre o RESULTADO FINAL da
        // busca, não por tentativa isolada. A versão anterior chamava GetByTelefoneAsync
        // direto em cada tentativa — que já resolve AMBIGUIDADE (2+) para null internamente —
        // então uma tentativa exata AMBÍGUA (null) era indistinguível de "não encontrado" (0) e
        // caía no fallback "55", que podia achar um TERCEIRO tutor completamente diferente do
        // par ambíguo original. Fix: conta ANTES de buscar (ContarAtivosPorTelefoneAsync,
        // mesmo Take(2) de GetByTelefoneAsync) para decidir corretamente — só tenta o fallback
        // quando a exata deu ZERO; se deu 2+ (ambíguo), o resultado FINAL já é "não encontrado"
        // e a busca PARA, sem tentar outra chave.
        var digitosEntrada = NormalizadorTelefone.ExtrairApenasDigitos(numero);
        var chaveBusca = digitosEntrada.Length == 0 ? numero : digitosEntrada;

        Tutor? tutor = null;
        var candidatosExatos = await _repository.ContarAtivosPorTelefoneAsync(chaveBusca);
        if (candidatosExatos == 1)
        {
            tutor = await _repository.GetByTelefoneAsync(chaveBusca);
        }
        else if (candidatosExatos == 0 && (digitosEntrada.Length == 10 || digitosEntrada.Length == 11))
        {
            var chaveFallback = "55" + digitosEntrada;
            var candidatosFallback = await _repository.ContarAtivosPorTelefoneAsync(chaveFallback);
            if (candidatosFallback == 1)
            {
                tutor = await _repository.GetByTelefoneAsync(chaveFallback);
            }
            // candidatosFallback == 0 ou 2+ ⇒ tutor permanece null (TASK-79 no fallback também).
        }
        // candidatosExatos >= 2 ⇒ tutor permanece null — AMBÍGUO é resultado final, sem fallback.

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
