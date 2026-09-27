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
    /// ⚠️ <b>Segunda lacuna conhecida e NÃO fechada aqui: não há guarda de data ao marcar falta.</b>
    /// Um agendamento marcado como <c>NAO_COMPARECEU</c> antes da hora marcada é aceito por esta
    /// máquina — «faltou» é sobre um compromisso que já passou, mas nada compara
    /// <c>DtAgendamento</c> com o relógio. Fechar isso exige uma ruling (tolerância? o horário do
    /// servidor ou o da clínica?) e mexe em fuso, que já mordeu este projeto; ficou fora do escopo
    /// da FD-06 de propósito, e está registrado no relatório da task.
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

    public AgendaService(
        IAgendamentoReadRepository readRepository,
        IClinicaContext clinicaContext,
        IAgendamentoRepository agendamentoRepository,
        IUnitOfWork uow,
        IGeradorUrlFotoPet geradorUrlFotoPet,
        ILogger<AgendaService> logger)
    {
        _readRepository = readRepository;
        _clinicaContext = clinicaContext;
        _agendamentoRepository = agendamentoRepository;
        _uow = uow;
        _geradorUrlFotoPet = geradorUrlFotoPet;
        _logger = logger;
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

        if (dto.NrVersion != agendamento.NrVersion)
            throw new ConflitoConcorrenciaException("Agendamento", id);

        agendamento.StStatus = dto.DsStatus;
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
