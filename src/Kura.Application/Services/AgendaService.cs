namespace Kura.Application.Services;

using Kura.Application.DTOs.Agenda;
using Kura.Application.Services.Interfaces;
using Kura.CrossCutting.Observability;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.Storage;
using Microsoft.Extensions.Logging;

public sealed class AgendaService : IAgendaService
{
    private readonly IAgendamentoReadRepository _readRepository;
    private readonly IAgendamentoRepository _agendamentoRepository;
    private readonly IClinicaContext _clinicaContext;
    private readonly IUnitOfWork _uow;
    private readonly IGeradorUrlFotoPet _geradorUrlFotoPet;
    private readonly ILogger<AgendaService> _logger;
    private readonly ITutorRepository _tutorRepository;
    private readonly IPetRepository _petRepository;
    private readonly IVeterinarioRepository _veterinarioRepository;
    private readonly ITriagemLunaRepository _triagemLunaRepository;
    private readonly IRelogioClinica _relogioClinica;

    /// <summary>
    /// FD-06 — <b>máquina de estados de <c>AGENDAMENTO.ST_STATUS</c> do lado <c>.NET</c>.</b>
    /// Chave = status atual da linha; valor = destinos alcançáveis a partir dele.
    ///
    /// <para>
    /// 🔴 <b>Por que a lista de destinos do validator NÃO basta.</b> O validator só enxerga o
    /// corpo da requisição; ele não sabe de onde o agendamento está saindo. Sem esta tabela,
    /// <c>StatusFinais</c> seria a única regra e <c>INTENCAO → REALIZADO</c> passaria com 200 —
    /// um atendimento faturável nascido de um lead que nunca virou agendamento. A trilha
    /// financeira deste mesmo ciclo (FD-10/FD-11) fatura exatamente sobre esse dado.
    /// </para>
    ///
    /// <para><b>As decisões, uma a uma:</b></para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>CONFIRMADO</c> só a partir de <c>AGENDADO</c> — cópia literal da guarda do outro
    ///     dono da tabela compartilhada (<c>Agendamento.java</c>, <c>confirmar()</c> exige
    ///     status <b>exatamente</b> <c>AGENDADO</c>). Divergir aqui faria o mesmo gesto ser
    ///     aceito por um backend e recusado pelo outro.
    ///   </description></item>
    ///   <item><description>
    ///     <c>NAO_COMPARECEU</c> a partir de <c>AGENDADO</c> ou <c>CONFIRMADO</c> — «faltou» só
    ///     tem sentido para quem tinha hora marcada. A partir de <c>INTENCAO</c> não: um lead
    ///     que nunca virou compromisso não pode faltar a ele, e aceitar isso encheria de faltas
    ///     falsas a base sobre a qual uma política de no-show seria construída.
    ///   </description></item>
    ///   <item><description>
    ///     <c>REALIZADO</c> a partir de <c>AGENDADO</c> ou <c>CONFIRMADO</c> — mesmo argumento,
    ///     e é o par que fecha <c>INTENCAO → REALIZADO</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <c>CANCELADO</c> a partir de <c>INTENCAO</c>, <c>AGENDADO</c> ou <c>CONFIRMADO</c> —
    ///     exatamente as três origens que o <c>cancelar()</c> do Java aceita depois da FD-06.
    ///     Cancelar um lead é legítimo: é como ele morre.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// 🔴 <b>ARMADILHA ARMADA PARA QUEM CRIAR O FLUXO DE LEAD — leia antes de usar
    /// <c>INTENCAO</c>.</b> Hoje <b>nenhuma linha pode estar em <c>INTENCAO</c></b>, e isso foi
    /// medido, não presumido: o backend Java grava apenas <c>AGENDADO</c> (em <c>criar()</c>),
    /// <c>CANCELADO</c> e <c>CONFIRMADO</c>; o <c>.NET</c> tem um único caminho de escrita (este),
    /// e o validator recusa <c>INTENCAO</c> com <b>400</b>; e o DDL nasce
    /// <c>DEFAULT 'AGENDADO'</c>. A linha <c>INTENCAO → CANCELADO</c> acima é, portanto, defesa em
    /// profundidade — não caminho vivo.
    /// </para>
    ///
    /// <para>
    /// <b>O que morde:</b> não existe aresta <c>INTENCAO → AGENDADO</c> em <b>nenhum</b> dos dois
    /// backends. Quem criar o fluxo de lead (Luna gerando intenção de agendamento, por exemplo)
    /// vai conseguir <b>gravar</b> <c>INTENCAO</c> pelo Java e depois descobrir que o lead só sabe
    /// morrer: daqui ele vai para <c>CANCELADO</c> e nada mais. <b>Promover lead a agendamento é
    /// uma decisão de produto que ninguém tomou ainda</b> — ela precisa de dono (qual backend
    /// escreve?) antes de virar aresta. Não acrescente <c>AGENDADO</c> aos destinos do validator
    /// só para destravar: o validator recusar estados de partida é deliberado (ver
    /// <c>AtualizarStatusAgendamentoValidator</c>).
    /// </para>
    ///
    /// <para>
    /// ✅ <b>Segunda lacuna — FECHADA pela REC-11 (era aberta na FD-06).</b> Um agendamento
    /// marcado como <c>NAO_COMPARECEU</c> antes da hora marcada NÃO é mais aceito: a guarda em
    /// <see cref="AtualizarStatusAsync"/> (bloco <c>if (dto.DsStatus == "NAO_COMPARECEU")</c>,
    /// linhas 238-249 deste arquivo na fix wave da REC-11 — confira com
    /// <c>grep -n "dto.DsStatus == \"NAO_COMPARECEU\""</c> antes de citar, regra 11 do
    /// workspace: linha que anda com o arquivo) compara <c>DtAgendamento</c> com
    /// <see cref="IRelogioClinica.Agora"/> (hora
    /// local de SP, mesma convenção de A-5) e recusa com 422 quando o relógio ainda não chegou
    /// lá. A mesma guarda também recusa quando já houve check-in OU início de atendimento (G2
    /// REC-11, I-1). Semântica: "a partir do horário marcado", inclusiva (o instante exato já
    /// aceita) — sem tolerância, porque nenhuma ruling de tolerância foi tomada; isso é
    /// declarado, não um defeito pendente.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Estado de origem desconhecido (ou nulo) é recusado, não ignorado.</b> A coluna é
    /// <c>NOT NULL DEFAULT 'AGENDADO'</c> com <c>CHECK</c> nos seis valores, então uma origem
    /// fora deste mapa é sinal de que o mapa envelheceu — e nesse caso a resposta certa é parar,
    /// não escolher um caminho.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> TransicoesPermitidas =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["INTENCAO"] = ["CANCELADO"],
            ["AGENDADO"] = ["CONFIRMADO", "REALIZADO", "CANCELADO", "NAO_COMPARECEU"],
            ["CONFIRMADO"] = ["REALIZADO", "CANCELADO", "NAO_COMPARECEU"],
            ["REALIZADO"] = [],
            ["CANCELADO"] = [],
            ["NAO_COMPARECEU"] = [],
        };

    /// <summary>
    /// Estados terminais — <b>derivados</b> do mapa acima (origem sem nenhum destino), nunca
    /// mantidos à mão.
    ///
    /// <para>
    /// 🔴 <b>É a regra de ouro v7 do projeto aplicada ao caso que a FD-06 criou.</b> Até esta
    /// task esta lista era <c>["REALIZADO", "CANCELADO"]</c> escrita literalmente, e estava
    /// certa por acidente: o validator tornava <c>NAO_COMPARECEU</c> <b>inalcançável</b>, então
    /// ninguém precisou lembrar dele aqui. Afrouxar o validator sem tocar nesta linha deixaria
    /// um agendamento marcado como falta virar <c>REALIZADO</c> depois — dado falso com cara de
    /// dado certo. Derivando, acrescentar um estado terminal ao mapa é suficiente: não há
    /// segunda lista para esquecer.
    /// </para>
    /// </summary>
    private static readonly IReadOnlySet<string> StatusFinais =
        TransicoesPermitidas
            .Where(par => par.Value.Length == 0)
            .Select(par => par.Key)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// REC-11 — status a partir dos quais check-in e início de atendimento são aceitos.
    ///
    /// <para>
    /// 🔴 <b>Check-in e início NÃO transicionam <c>ST_STATUS</c> (A-2).</b> Eles escrevem
    /// <c>DT_CHECKIN</c>/<c>DT_INICIO_ATENDIMENTO</c>, colunas irmãs, nunca a máquina de estados
    /// de <see cref="TransicoesPermitidas"/> — por isso este é um segundo conjunto, não um
    /// reaproveitamento de <see cref="StatusFinais"/>/<c>TransicoesPermitidas</c>. Um agendamento
    /// segue <c>AGENDADO</c> (ou <c>CONFIRMADO</c>) depois do check-in; é <c>DsEtapaRecepcao</c>
    /// (<see cref="CalcularEtapaRecepcao"/>) quem muda, lendo os timestamps.
    /// </para>
    /// </summary>
    private static readonly IReadOnlySet<string> StatusElegiveisParaEventoRecepcao =
        new HashSet<string>(StringComparer.Ordinal) { "AGENDADO", "CONFIRMADO" };

    public AgendaService(
        IAgendamentoReadRepository readRepository,
        IClinicaContext clinicaContext,
        IAgendamentoRepository agendamentoRepository,
        IUnitOfWork uow,
        IGeradorUrlFotoPet geradorUrlFotoPet,
        ILogger<AgendaService> logger,
        ITutorRepository tutorRepository,
        IPetRepository petRepository,
        IVeterinarioRepository veterinarioRepository,
        ITriagemLunaRepository triagemLunaRepository,
        IRelogioClinica relogioClinica)
    {
        _readRepository = readRepository;
        _clinicaContext = clinicaContext;
        _agendamentoRepository = agendamentoRepository;
        _uow = uow;
        _geradorUrlFotoPet = geradorUrlFotoPet;
        _logger = logger;
        _tutorRepository = tutorRepository;
        _petRepository = petRepository;
        _veterinarioRepository = veterinarioRepository;
        _triagemLunaRepository = triagemLunaRepository;
        _relogioClinica = relogioClinica;
    }

    public async Task<AgendaResponseDto> GetAgendaAsync(
        DateTime dataInicio, DateTime dataFim, long? idVeterinario)
    {
        // S3D-04b: span-filho de camada Application, aninhado sob o span HTTP
        // (AddAspNetCoreInstrumentation, S3D-04) por Activity.Current — sem passagem
        // manual de contexto, é o comportamento padrão do System.Diagnostics.Activity.
        using var activity = KuraActivitySource.Instancia.StartActivity("Application.AgendaService.GetAgendaAsync");
        activity?.SetTag("kura.layer", "Application");
        activity?.SetTag("kura.intervalo_dias", (dataFim - dataInicio).TotalDays);

        if (dataFim < dataInicio)
            throw new RegraDeNegocioException("DataFim não pode ser anterior à DataInicio.");

        if ((dataFim - dataInicio).TotalDays > 31)
            throw new RegraDeNegocioException("Intervalo máximo de 31 dias.");

        var agendamentos = await _readRepository.GetByIntervaloAsync(
            _clinicaContext.IdClinica, dataInicio, dataFim, idVeterinario);

        var itens = agendamentos.Select(ToItemDto).ToList();

        return new AgendaResponseDto
        {
            DataInicio = dataInicio,
            DataFim = dataFim,
            Agendamentos = itens
        };
    }

    public async Task<AgendamentoItemDto> AtualizarStatusAsync(long id, AtualizarStatusAgendamentoDto dto)
    {
        var agendamento = await _agendamentoRepository.GetByIdAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Agendamento", id);

        if (agendamento.StStatus is not null && StatusFinais.Contains(agendamento.StStatus))
            throw new RegraDeNegocioException(
                $"Agendamento {id} já está em estado final ({agendamento.StStatus}) e não pode ser alterado.");

        // FD-06 — a origem manda tanto quanto o destino. Ver TransicoesPermitidas.
        if (agendamento.StStatus is null
            || !TransicoesPermitidas.TryGetValue(agendamento.StStatus, out var destinosPermitidos))
            throw new RegraDeNegocioException(
                $"Agendamento {id} está com status atual não reconhecido "
                + $"('{agendamento.StStatus ?? "null"}') e não pode ter o status alterado.");

        if (!destinosPermitidos.Contains(dto.DsStatus, StringComparer.Ordinal))
            throw new RegraDeNegocioException(
                $"Transição de status inválida para o agendamento {id}: "
                + $"{agendamento.StStatus} -> {dto.DsStatus}. A partir de {agendamento.StStatus} "
                + $"só é possível ir para: {string.Join(", ", destinosPermitidos)}.");

        // REC-11 — fecha a lacuna declarada acima (linhas 83-90 da doc de TransicoesPermitidas):
        // falta só existe DEPOIS do horário marcado e NUNCA depois de check-in OU início de
        // atendimento (quem chegou -- ou já está sendo atendido -- não faltou). Guarda de
        // negócio, então vem ANTES do conflito de versão — mesma ordem já estabelecida para
        // "transição inválida precede conflito de versão".
        //
        // G2 REC-11 (I-1): a guarda original olhava só DtCheckin. Um walk-in que entra direto
        // (IniciarAtendimentoAsync sem check-in prévio, aceite explícito da REC-11) tem
        // DtInicioAtendimento preenchido e DtCheckin nulo -- e por isso passava por esta guarda
        // e virava NAO_COMPARECEU com 200 (medido pela G2, S5). Quem está sendo atendido, com
        // muito mais razão do que quem só chegou, não faltou.
        if (dto.DsStatus == "NAO_COMPARECEU")
        {
            if (agendamento.DtCheckin.HasValue || agendamento.DtInicioAtendimento.HasValue)
                throw new RegraDeNegocioException(
                    $"Agendamento {id} já teve check-in ou início de atendimento registrado "
                    + $"({agendamento.DtCheckin:yyyy-MM-dd HH:mm}/{agendamento.DtInicioAtendimento:yyyy-MM-dd HH:mm}) "
                    + "e não pode ser marcado como falta.");

            var agoraFalta = _relogioClinica.Agora();
            if (agoraFalta < agendamento.DtAgendamento)
                throw new RegraDeNegocioException(
                    $"Agendamento {id} está marcado para {agendamento.DtAgendamento:yyyy-MM-dd HH:mm} "
                    + "e ainda não chegou a esse horário — falta só pode ser registrada depois do "
                    + "horário marcado.");
        }

        if (dto.NrVersion != agendamento.NrVersion)
            throw new ConflitoConcorrenciaException("Agendamento", id);

        agendamento.StStatus = dto.DsStatus;
        agendamento.NrVersion = dto.NrVersion + 1;

        _agendamentoRepository.Update(agendamento);
        await _uow.CommitAsync();
        return ToItemDto(agendamento);
    }

    /// <summary>
    /// REC-10 — <c>POST /api/v1/agendamentos</c>. Validações relacionais copiadas do Java
    /// COM ÂNCORA (<c>backend-tutor-java</c> @ <c>d1522ee</c>,
    /// <c>AgendamentoService.criar</c> :64-90 e <c>Agendamento.criar</c> :96-112 —
    /// corrigido pela G2 (m-1, era :91-106; conferido de novo em 2026-09-27 com
    /// <c>git show d1522ee:.../Agendamento.java | grep -n "public static Agendamento criar\|return a;"</c>
    /// ⇒ 96 e 112) + o que o G0
    /// item 9 mediu como AUSENTE no Java e o REC-10 adiciona por decisão do backlog:
    /// <list type="bullet">
    ///   <item><description><b>Tutor</b> — deve existir, estar ativo e ser da clínica do
    ///   JWT (<c>ITutorRepository.GetByIdAsync(id, idClinica)</c>, já explícito no
    ///   predicado). Espelha <c>tutorRepository.findByIdTutorAndStAtivo</c>.</description></item>
    ///   <item><description><b>Pet</b> — mesma forma (clínica do JWT, ativo via
    ///   HasQueryFilter). Espelha <c>petRepository.findByIdPetAndStAtivo</c>.</description></item>
    ///   <item><description><b>Pet vinculado ao tutor</b> — via <c>TutorPets</c> carregado
    ///   junto. Espelha o <c>anyMatch</c> do Java; aqui vira 422 (RegraDeNegocioException),
    ///   não 403 (este projeto não mapeia Forbidden para esta classe de erro).</description></item>
    ///   <item><description><b>Veterinário da clínica do JWT</b> — ALÉM do Java (G0 item 9:
    ///   "idVeterinario não é validado... vet de OUTRA clínica é aceito" no lado
    ///   Java). Decisão do backlog: REC-10 é mais estrita aqui.</description></item>
    ///   <item><description><b>Triagem de origem</b> (opcional, F-3) — da clínica do JWT E
    ///   do MESMO tutor (G2/m-4: nada no banco amarra as duas FKs). Triagem de outra
    ///   clínica ⇒ mesma resposta do inexistente (404); triagem de outro tutor da MESMA
    ///   clínica ⇒ 422 (resposta diferente, de propósito — a existência da triagem não
    ///   vaza, só a discordância de tutor).</description></item>
    ///   <item><description><b>Encaixe</b> (ruling do backlog, adapta o
    ///   <c>@Future</c>/<c>isBefore(now)</c> do Java): até 15 minutos no passado são
    ///   aceitos, usando <see cref="IRelogioClinica"/> (hora local de SP, REC-08) — nunca
    ///   <c>DateTime.UtcNow</c>.</description></item>
    /// </list>
    /// <b>NÃO copiado, de propósito (declarado, não esquecido):</b> checagem de
    /// sobreposição de horário — o Java tem a query pronta
    /// (<c>buscarPorVeterinarioEIntervalo</c>) mas NENHUM chamador (G0 item 9,
    /// <c>git grep</c> só acha a declaração); o backlog proíbe regra que o Java não tem, e
    /// o encaixe depende de permitir sobreposição.
    /// </summary>
    public async Task<AgendamentoItemDto> CriarAsync(AgendamentoCreateDto dto)
    {
        var idClinica = _clinicaContext.IdClinica;

        // G2-REC10/m-6 — DECLARADO: os 4 predicados explícitos abaixo (Tutor/Pet/
        // Veterinario/TriagemLuna, todos "Id == id && IdClinica == idClinica") NÃO
        // incluem "ativo" — quem garante isso hoje é só o `HasQueryFilter` global
        // (KuraDbContext.ApplyTenantFilters, que combina StAtiva && IdClinica==filtro
        // para as 4 entidades). Sob JWT de clínica (único caminho deste endpoint hoje,
        // [Authorize]) isso é defesa em profundidade parcial, não um buraco: o filtro
        // global está sempre ligado aqui. Se algum consumidor futuro deste service
        // reaproveitar estes repositórios FORA de um contexto com JWT (filtro global
        // desligado — ver G2-REC10/m-2 abaixo), tutor/pet/veterinário/triagem
        // inativos deixariam de ser barrados. Sem teste que exija hoje — só
        // declarado, por decisão do G2 (m-6, Minor).
        var tutor = await _tutorRepository.GetByIdAsync(dto.IdTutor, idClinica)
            ?? throw new EntidadeNaoEncontradaException("Tutor", dto.IdTutor);

        var pet = await _petRepository.GetByIdComVinculosAsync(dto.IdPet, idClinica)
            ?? throw new EntidadeNaoEncontradaException("Pet", dto.IdPet);

        if (!pet.TutorPets.Any(tp => tp.IdTutor == dto.IdTutor))
            throw new RegraDeNegocioException(
                $"Pet {dto.IdPet} não está vinculado ao tutor {dto.IdTutor}.");

        var veterinario = await _veterinarioRepository.GetByIdAsync(dto.IdVeterinario, idClinica)
            ?? throw new EntidadeNaoEncontradaException("Veterinario", dto.IdVeterinario);

        Domain.Entities.TriagemLuna? triagemOrigem = null;
        if (dto.IdTriagemOrigem.HasValue)
        {
            triagemOrigem = await _triagemLunaRepository.GetByIdAsync(dto.IdTriagemOrigem.Value, idClinica)
                ?? throw new EntidadeNaoEncontradaException("TriagemLuna", dto.IdTriagemOrigem.Value);

            // G2/m-4 (REC-09) — a FK (ID_TRIAGEM_ORIGEM -> TRIAGEM_LUNA) não amarra o tutor;
            // quem amarra é esta comparação explícita. Triagem já confirmada da MESMA
            // clínica acima (senão teria caído no 404 de cima) — aqui só falta o tutor.
            if (triagemOrigem.IdTutor != dto.IdTutor)
                throw new RegraDeNegocioException(
                    $"Triagem {dto.IdTriagemOrigem} não pertence ao tutor {dto.IdTutor}.");

            // G2-REC10/m-5 — DELIBERADO: nada aqui impede a MESMA triagem de originar N
            // agendamentos (medido pela G2: POST duas vezes com o mesmo idTriagemOrigem ⇒
            // 201, 201, dois agendamentos distintos com o mesmo ID_TRIAGEM_ORIGEM).
            // Ruling do maestro: permitido de propósito — a recepção pode legitimamente
            // reagendar/desdobrar um mesmo atendimento de triagem em mais de uma consulta
            // (ex.: encaixe de emergência + retorno agendado a partir da mesma triagem), e
            // não há requisito de produto que torne a triagem um recurso "consumível" 1:1.
            // Não confundir com sobreposição de horário do MESMO veterinário, que também
            // não é checada nesta v1 (ver o comentário de classe acima) — são preocupações
            // independentes.
        }

        var agora = _relogioClinica.Agora();
        if (dto.DtAgendamento < agora.AddMinutes(-15))
            throw new RegraDeNegocioException(
                "DtAgendamento não pode ser mais de 15 minutos no passado (tolerância de encaixe).");

        var agendamento = new Domain.Entities.Agendamento
        {
            IdClinica = idClinica,
            IdTutor = dto.IdTutor,
            IdPet = dto.IdPet,
            IdVeterinario = dto.IdVeterinario,
            DtAgendamento = dto.DtAgendamento,
            NrDuracaoMinutos = dto.Duracao ?? 30,
            DsTipoConsulta = dto.DsTipo,
            DsObservacoes = dto.DsObservacoes,
            StStatus = "AGENDADO",
            // A-1/m-1: SEMPRE explícito — DS_ORIGEM é NOT NULL DEFAULT 'PORTAL', e um
            // NULL explícito no INSERT dá ORA-01400 (o DEFAULT do Oracle só se aplica
            // quando a coluna é OMITIDA, nunca a um NULL explícito).
            DsOrigem = triagemOrigem is not null ? "TRIAGEM_LUNA" : "RECEPCAO",
            IdTriagemOrigem = triagemOrigem?.Id,
            NrVersion = 0,
            DtCriacao = agora,
            // Navegações explícitas: as 4 entidades já vieram TRACKED do mesmo
            // DbContext (buscas acima), então isto não dispara INSERT duplicado — só
            // popula o grafo para ToItemDto devolver nome/urgência sem round trip extra.
            Tutor = tutor,
            Pet = pet,
            Veterinario = veterinario,
            TriagemOrigem = triagemOrigem,
        };

        await _agendamentoRepository.AddAsync(agendamento);
        await _uow.CommitAsync();

        // PK: HasDefaultValueSql("SEQ_AGENDAMENTO.NEXTVAL") faz o EF omitir a coluna no
        // INSERT (A-4) -- ver AgendamentoConfiguration e AgendamentoPkStrategyTests. O
        // provider InMemory usado nos testes gera seu próprio Id (não zero) após o
        // CommitAsync acima; contra Oracle real, quem gera é a sequence (G4).
        return ToItemDto(agendamento);
    }

    /// <summary>
    /// REC-11 — <c>POST /api/v1/agendamentos/{id}/checkin</c>. Registra a chegada do paciente
    /// (<c>DT_CHECKIN</c>), nunca muda <c>ST_STATUS</c> (A-2).
    ///
    /// <para><b>Ordem das guardas</b> (mesmo raciocínio de <see cref="AtualizarStatusAsync"/>:
    /// guarda de negócio antes de conflito de versão), fixada na fix wave do G2 REC-11: (1) 404
    /// se não achar, escopado por <see cref="IClinicaContext"/> (A-7); (2) status fora de
    /// <see cref="StatusElegiveisParaEventoRecepcao"/> ⇒ 422; (3) IDEMPOTÊNCIA — já tem
    /// <c>DT_CHECKIN</c> ⇒ devolve o estado atual, SEM avaliar nenhuma guarda abaixo (não há
    /// escrita, OCC/data/ordem não se aplicam a uma leitura); (4) G2/m-1 — já tem
    /// <c>DT_INICIO_ATENDIMENTO</c> (walk-in que começou sem check-in) ⇒ 422, check-in depois do
    /// início inverteria a linha do tempo e a espera por linha (A-6) sairia negativa; (5) G2/m-2
    /// — <see cref="IRelogioClinica.Hoje"/> diferente do dia de <c>DtAgendamento</c> ⇒ 422
    /// (nem véspera nem D+1: check-in só no dia certo); (6) versão divergente ⇒ 409; (7) grava
    /// <see cref="IRelogioClinica.Agora"/>, incrementa <c>NrVersion</c>, commita.</para>
    /// </summary>
    public async Task<AgendamentoItemDto> CheckinAsync(long id, RegistrarEventoRecepcaoDto dto)
    {
        var agendamento = await _agendamentoRepository.GetByIdAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Agendamento", id);

        if (agendamento.StStatus is null || !StatusElegiveisParaEventoRecepcao.Contains(agendamento.StStatus))
            throw new RegraDeNegocioException(
                $"Agendamento {id} está com status '{agendamento.StStatus ?? "null"}' e não pode "
                + "receber check-in. Só é possível fazer check-in a partir de AGENDADO ou "
                + "CONFIRMADO.");

        if (agendamento.DtCheckin.HasValue)
            return ToItemDto(agendamento);

        // G2 REC-11 (m-1): sem isto, um walk-in que já começou o atendimento (sem check-in
        // prévio, aceite legítimo da própria REC-11) podia "checar-in" DEPOIS do início --
        // DT_CHECKIN > DT_INICIO_ATENDIMENTO, e a espera por linha (A-6) sairia negativa.
        if (agendamento.DtInicioAtendimento.HasValue)
            throw new RegraDeNegocioException(
                $"Agendamento {id} já teve início de atendimento registrado em "
                + $"{agendamento.DtInicioAtendimento:yyyy-MM-dd HH:mm} e não pode receber "
                + "check-in depois disso.");

        // G2 REC-11 (m-2): check-in só no DIA do agendamento -- nem véspera, nem D+1. Sem
        // guarda nenhuma antes desta fix wave, um toque errado na linha de outro dia registrava
        // chegada (S9 da G2: agendamento de daqui a 7 dias aceitava check-in).
        if (_relogioClinica.Hoje() != agendamento.DtAgendamento.Date)
            throw new RegraDeNegocioException(
                $"Agendamento {id} está marcado para {agendamento.DtAgendamento:yyyy-MM-dd} "
                + "e só pode receber check-in no dia do agendamento.");

        if (dto.NrVersion != agendamento.NrVersion)
            throw new ConflitoConcorrenciaException("Agendamento", id);

        agendamento.DtCheckin = _relogioClinica.Agora();
        agendamento.NrVersion = dto.NrVersion + 1;

        _agendamentoRepository.Update(agendamento);
        await _uow.CommitAsync();
        return ToItemDto(agendamento);
    }

    /// <summary>
    /// REC-11 — <c>POST /api/v1/agendamentos/{id}/inicio-atendimento</c>. Registra o início do
    /// atendimento (<c>DT_INICIO_ATENDIMENTO</c>), permitido SEM check-in prévio (walk-in que
    /// entra direto) — este método nunca toca <c>DT_CHECKIN</c>. Mesma ordem de guardas de
    /// <see cref="CheckinAsync"/> (sem o m-1 -- não existe um "início depois de início" a
    /// bloquear; a mesma guarda de data do G2/m-2 se aplica).
    /// </summary>
    public async Task<AgendamentoItemDto> IniciarAtendimentoAsync(long id, RegistrarEventoRecepcaoDto dto)
    {
        var agendamento = await _agendamentoRepository.GetByIdAsync(id, _clinicaContext.IdClinica)
            ?? throw new EntidadeNaoEncontradaException("Agendamento", id);

        if (agendamento.StStatus is null || !StatusElegiveisParaEventoRecepcao.Contains(agendamento.StStatus))
            throw new RegraDeNegocioException(
                $"Agendamento {id} está com status '{agendamento.StStatus ?? "null"}' e não pode "
                + "iniciar atendimento. Só é possível iniciar a partir de AGENDADO ou CONFIRMADO.");

        if (agendamento.DtInicioAtendimento.HasValue)
            return ToItemDto(agendamento);

        // G2 REC-11 (m-2, mesma regra do check-in): início só no DIA do agendamento.
        if (_relogioClinica.Hoje() != agendamento.DtAgendamento.Date)
            throw new RegraDeNegocioException(
                $"Agendamento {id} está marcado para {agendamento.DtAgendamento:yyyy-MM-dd} "
                + "e só pode iniciar atendimento no dia do agendamento.");

        if (dto.NrVersion != agendamento.NrVersion)
            throw new ConflitoConcorrenciaException("Agendamento", id);

        // NÃO inventa DT_CHECKIN -- walk-in que entra direto continua com DtCheckin null.
        agendamento.DtInicioAtendimento = _relogioClinica.Agora();
        agendamento.NrVersion = dto.NrVersion + 1;

        _agendamentoRepository.Update(agendamento);
        await _uow.CommitAsync();
        return ToItemDto(agendamento);
    }

    private AgendamentoItemDto ToItemDto(Domain.Entities.Agendamento a) => new()
    {
        IdAgendamento = a.Id,
        DtAgendamento = a.DtAgendamento,
        DuracaoMinutos = a.NrDuracaoMinutos ?? 0,
        NmTutor = a.Tutor?.NmTutor ?? string.Empty,
        NmPet = a.Pet?.NmPet ?? string.Empty,
        IdVeterinario = a.IdVeterinario ?? 0,
        NmVeterinario = a.Veterinario?.NmVeterinario ?? string.Empty,
        DsTipoConsulta = a.DsTipoConsulta ?? string.Empty,
        DsStatus = a.StStatus ?? string.Empty,
        NrVersion = a.NrVersion,
        IdPet = a.IdPet,
        IdTutor = a.IdTutor,
        DtCheckin = a.DtCheckin,
        DtInicioAtendimento = a.DtInicioAtendimento,
        DsOrigem = a.DsOrigem,
        // A-7: DsNivelUrgenciaOrigem só existe quando TriagemOrigem foi carregada pelo
        // Include — e o HasQueryFilter de TriagemLuna (KuraDbContext) já garante que uma
        // triagem de outra clínica nunca chega aqui (fica null, não a linha errada).
        DsNivelUrgenciaOrigem = a.TriagemOrigem?.DsNivelUrgencia,
        DsRespostaConfirmacao = a.DsRespostaConfirmacao,
        DsEtapaRecepcao = CalcularEtapaRecepcao(a.StStatus, a.DtCheckin, a.DtInicioAtendimento),
        DsFotoThumbUrl = GerarFotoThumbUrlSeguro(a)
    };

    /// <summary>
    /// G2/m-6 — <c>IGeradorUrlFotoPet.GerarUrl</c> LANÇA (<see cref="ArgumentException"/> via
    /// <c>ChaveFotoPet.Variante</c>) quando <c>Pet.DsFotoChave</c> não tem extensão — uma
    /// chave que nunca deveria existir (todo produtor real passa por
    /// <c>ChaveFotoPet.Base()</c>), mas que a G2 mediu ao vivo por HTTP: **uma** linha com
    /// chave malformada derrubava o <c>GET /agenda</c> inteiro da clínica com <c>500</c>,
    /// porque a chamada estava dentro do <c>Select</c> sem proteção. Uma foto ruim não pode
    /// derrubar a lista inteira de agendamentos do dia — o card daquele pet específico fica
    /// sem foto (mesmo comportamento de "pet sem foto"), o resto da agenda continua de pé.
    ///
    /// <para><b>Sem chave nem PII no log</b> (ruling do fix wave) — só os ids numéricos do
    /// agendamento e do pet, que não identificam paciente/tutor por si sós.</para>
    /// </summary>
    private string? GerarFotoThumbUrlSeguro(Domain.Entities.Agendamento a)
    {
        try
        {
            return _geradorUrlFotoPet.GerarUrl(a.Pet?.DsFotoChave, ChaveFotoPet.SufixoThumb);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falha ao gerar URL de foto para o agendamento {IdAgendamento} (pet {IdPet}) -- DsFotoThumbUrl sai null, o resto da agenda continua.",
                a.Id, a.IdPet);
            return null;
        }
    }

    /// <summary>
    /// A-3 — deriva a etapa de recepção NO SERVIDOR, num lugar só. Função pura (sem I/O,
    /// sem dependência de infraestrutura), testável diretamente por tabela-verdade
    /// (aceite (a) da REC-09). Reaproveita <see cref="StatusFinais"/> — a mesma fonte de
    /// verdade que bloqueia transições terminais em <see cref="AtualizarStatusAsync"/> —
    /// em vez de duplicar a lista de estados terminais (regra de ouro v7: inventário à
    /// mão apodrece em silêncio).
    ///
    /// <para><b>Precedência (da mais forte para a mais fraca), medida contra o desenho da
    /// REC-09 (A-3):</b></para>
    /// <list type="number">
    ///   <item><description><b>Status terminal</b> (<see cref="StatusFinais"/>) —
    ///   <c>REALIZADO</c> vira <c>FINALIZADO</c> para a recepção; <c>CANCELADO</c> e
    ///   <c>NAO_COMPARECEU</c> mantêm o próprio nome. Este passo vem ANTES dos timestamps
    ///   de propósito: um check-in tardio batido num agendamento já cancelado não pode
    ///   reabrir a etapa (aceite explícito da REC-09) — <c>DsEtapaRecepcao</c> reflete o
    ///   destino da máquina de estados, não o relógio de operação.</description></item>
    ///   <item><description><c>DT_INICIO_ATENDIMENTO</c> presente ⇒
    ///   <c>EM_ATENDIMENTO</c>. Cobre também o walk-in que entra direto sem check-in
    ///   prévio ("início sem check-in", aceite explícito da REC-09) — não exige
    ///   <c>DT_CHECKIN</c> preenchido.</description></item>
    ///   <item><description><c>DT_CHECKIN</c> presente (e sem início de atendimento) ⇒
    ///   <c>CHEGOU</c>.</description></item>
    ///   <item><description>Nenhum dos anteriores: o próprio <c>ST_STATUS</c> —
    ///   <c>CONFIRMADO</c> permanece <c>CONFIRMADO</c>; qualquer outro estado
    ///   não-terminal (inclui <c>AGENDADO</c> e o defensivo <c>INTENCAO</c>, que a FD-06
    ///   documenta como inalcançável em produção — nenhum backend grava essa
    ///   origem) cai em <c>AGENDADO</c>.</description></item>
    /// </list>
    /// </summary>
    public static string CalcularEtapaRecepcao(
        string? stStatus, DateTime? dtCheckin, DateTime? dtInicioAtendimento)
    {
        if (stStatus is not null && StatusFinais.Contains(stStatus))
            return stStatus == "REALIZADO" ? "FINALIZADO" : stStatus;

        if (dtInicioAtendimento.HasValue)
            return "EM_ATENDIMENTO";

        if (dtCheckin.HasValue)
            return "CHEGOU";

        return stStatus == "CONFIRMADO" ? "CONFIRMADO" : "AGENDADO";
    }
}
