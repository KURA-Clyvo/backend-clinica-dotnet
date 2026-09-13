namespace Kura.Application.Services;

using System.Text;
using Kura.Application.DTOs.Common;
using Kura.Application.DTOs.Luna;
using Kura.Application.Services.Interfaces;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.ValueObjects;

public sealed class LunaService : ILunaService
{
    private const int MaxIntervaloDias = 90;

    // DS_CONTEUDO é VARCHAR2(4000) NOT NULL (V15__interacao_canal.sql). Truncar em vez
    // de rejeitar: o objetivo deste backlog é parar de perder mensagem de WhatsApp
    // silenciosamente — devolver 422 para uma mensagem longa recria exatamente esse
    // sintoma (a Luna cai no except genérico e a interação nunca é persistida). Perder
    // a cauda de uma mensagem rara acima do teto é preferível a perder o registro
    // inteiro. ds_conteudo nunca é vazio aqui (InteractionRequestValidator.NotEmpty()),
    // então truncar não reintroduz o bug '' -> NULL.
    //
    // TASK-67 fix round 1 (Important-2 da revisão): "4000" aqui é o teto em BYTES da
    // coluna, não em caracteres. VARCHAR2(4000) sem "CHAR" herda NLS_LENGTH_SEMANTICS,
    // cujo default Oracle é BYTE; num banco AL32UTF8 (o do compose) cada acentuado
    // custa 2 bytes e cada emoji até 4. Truncar em 4000 *caracteres* podia estourar
    // 4000 *bytes* com uma mensagem de WhatsApp acentuada (WhatsApp aceita até 4096
    // chars) → ORA-12899 → 500 — a mesma classe de bug Oracle-only do FIX_4, que
    // InMemory nunca reproduz. TruncarPorBytesUtf8 corta por Rune (nunca no meio de um
    // caractere multibyte/par substituto) e mede o resultado em bytes UTF-8 de verdade.
    private const int MaxTamanhoConteudoBytes = 4000;

    // DS_DESCRICAO é VARCHAR2(2000) NOT NULL (V9__schema_drift_clinico.sql) e é onde
    // sintomas[]/nr_score/ds_recomendacao são compostos (decisão 2, ver RegistrarTriagemAsync).
    // Mesmo raciocínio de bytes-vs-caracteres do MaxTamanhoConteudoBytes acima.
    private const int MaxTamanhoDescricaoTriagemBytes = 2000;

    // DS_SINTOMAS é VARCHAR2(1000) NULLABLE (V21, backend-tutor-java, em paralelo —
    // LU-02). Mesmo raciocínio de bytes-vs-caracteres das duas constantes acima.
    private const int MaxTamanhoSintomasBytes = 1000;

    // Delimitador documentado do texto gravado em DS_SINTOMAS — ver o comentário de
    // TriagemLuna.DsSintomas para o porquê de ';' e não JSON/CSV.
    private const string DelimitadorSintomas = ";";

    private const string MarcadorTruncamento = "…[truncado]";

    // LU-08: paginação de GET /api/v1/luna/triagens — mesmo teto/clamp de
    // MedicamentoService.ListarAsync (o único outro endpoint paginado do repo).
    private const int PageSizeMaximo = 100;

    private readonly ITriagemLunaRepository _triagemRepository;
    private readonly IRepository<InteracaoCanal> _interacaoRepository;
    private readonly ITutorRepository _tutorRepository;
    private readonly IUnitOfWork _uow;

    // LU-08: única dependência nova do service. Os 3 endpoints TASK-67
    // (interactions/triage/relatório histórico) são chamados sem JWT de clínica — só
    // GET /triagens usa isto, e só ele pode (é o único [Authorize] simples dos 4).
    private readonly IClinicaContext _clinicaContext;

    public LunaService(
        ITriagemLunaRepository triagemRepository,
        IRepository<InteracaoCanal> interacaoRepository,
        ITutorRepository tutorRepository,
        IUnitOfWork uow,
        IClinicaContext clinicaContext)
    {
        _triagemRepository = triagemRepository;
        _interacaoRepository = interacaoRepository;
        _tutorRepository = tutorRepository;
        _uow = uow;
        _clinicaContext = clinicaContext;
    }

    public async Task<RelatorioTriagensDto> GerarRelatorioAsync(DateTime dataInicio, DateTime dataFim)
    {
        ValidarPeriodo(dataInicio, dataFim);

        var triagens = await _triagemRepository.GetByIntervaloAsync(dataInicio, dataFim);

        var porUrgencia = triagens
            .GroupBy(t => t.DsNivelUrgencia)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RelatorioTriagensDto
        {
            DataInicio = dataInicio,
            DataFim = dataFim,
            TotalTriagens = triagens.Count,
            PorUrgencia = porUrgencia,
            EncaminhadasParaVet = triagens.Count(t => t.StEncaminhadoVet)
        };
    }

    /// <summary>
    /// LU-08: GET /api/v1/luna/triagens. idClinica vem SEMPRE de IClinicaContext (o
    /// token JWT), nunca de query string — não existe parâmetro de clínica na
    /// assinatura pública deste método de propósito, para tornar IDOR por
    /// clinicaId=outroTenant estruturalmente impossível aqui (diferente de
    /// VeterinariosController.GetAll pré-R2, que aceitava e ignorava — aqui o
    /// parâmetro nem existe). page/pageSize seguem o mesmo clamp de
    /// MedicamentoService.ListarAsync. Período opcional: quando os dois extremos são
    /// informados, valida o mesmo teto de 90 dias do relatório (ValidarPeriodo); só um
    /// dos dois informado filtra em aberto de um dos lados, sem teto (não há como
    /// violar "90 dias" com um intervalo que não tem os dois extremos).
    /// </summary>
    public async Task<PagedResultDto<TriagemListaItemDto>> ListarTriagensAsync(
        string? urgencia,
        DateTime? dataInicio,
        DateTime? dataFim,
        int page,
        int pageSize)
    {
        if (dataInicio.HasValue && dataFim.HasValue)
            ValidarPeriodo(dataInicio.Value, dataFim.Value);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, PageSizeMaximo);

        var (itens, total) = await _triagemRepository.ListarPorClinicaAsync(
            _clinicaContext.IdClinica, urgencia, dataInicio, dataFim, page, pageSize);

        return new PagedResultDto<TriagemListaItemDto>
        {
            Items = itens.Select(MapearItemLista),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static TriagemListaItemDto MapearItemLista(TriagemListaItem item) =>
        new()
        {
            IdTriagem = item.IdTriagem,
            DtTriagem = item.DtTriagem,
            Urgencia = item.Urgencia,
            Sintomas = [.. item.Sintomas],
            Score = item.Score,
            RegrasVersao = item.RegrasVersao,
            EncaminhadoVet = item.EncaminhadoVet,
            Tutor = item.IdTutor.HasValue
                ? new TutorTriagemDto { Id = item.IdTutor.Value, Nome = item.NomeTutor ?? string.Empty }
                : null,
            Pets = [.. item.Pets.Select(p => new PetTriagemDto { Id = p.IdPet, Nome = p.NmPet, Especie = p.NmEspecie })],
            TrechoMensagem = item.TrechoMensagem
        };

    private static void ValidarPeriodo(DateTime dataInicio, DateTime dataFim)
    {
        if (dataFim < dataInicio)
            throw new RegraDeNegocioException("DataFim não pode ser anterior à DataInicio.");

        if ((dataFim - dataInicio).TotalDays > MaxIntervaloDias)
            throw new RegraDeNegocioException($"Intervalo máximo de {MaxIntervaloDias} dias.");
    }

    public async Task<InteractionResponseDto> RegistrarInteracaoAsync(InteractionRequestDto dto)
    {
        // Decisão 1 (TASK-77, FIX_7 — decisão de produto do Felipe, substitui a decisão 1
        // original da TASK-67): telefone não cadastrado (id_tutor null no payload da
        // Luna) deixa de ser rejeitado com 422 e passa a GRAVAR a interação, com
        // IdClinica/IdTutor nulos. A rejeição antiga perdia o registro inteiro (nenhuma
        // INTERACAO_CANAL gravada) e ainda gerava um erro FALSO em LOG_ERRO do lado da
        // Luna — dois efeitos colaterais ruins para um caso que não é excepcional (tutor
        // desconhecido é esperado no fluxo real de WhatsApp). Viável hoje porque
        // INTERACAO_CANAL.ID_CLINICA é nullable desde
        // V16__interacao_canal_clinica_nullable.sql (backend-tutor-java, TASK-76).
        //
        // Consequência aceita e documentada (não é bug): uma linha com IdClinica nulo é
        // invisível para qualquer leitura escopada por clínica (ver
        // KuraDbContext.ApplyTenantFilters) — o ganho aqui é auditoria/parar o erro falso
        // em LOG_ERRO, não visibilidade no app da clínica. Não construir tela/consulta de
        // "triagem não atribuída" — fora de escopo desta task.
        //
        // Quando id_tutor vem preenchido, o comportamento é idêntico ao da TASK-67:
        // tutor inexistente ainda lança 404 (não vira registro anônimo silenciosamente —
        // um id_tutor que a Luna mandou e não existe é sinal de payload inconsistente,
        // diferente de id_tutor ausente por não cadastro).
        Tutor? tutor = null;
        if (dto.IdTutor is not null)
        {
            tutor = await _tutorRepository.GetByIdAsync(dto.IdTutor.Value)
                ?? throw new EntidadeNaoEncontradaException("Tutor", dto.IdTutor.Value);
        }

        var conteudo = TruncarPorBytesUtf8(dto.DsConteudo, MaxTamanhoConteudoBytes);

        var interacao = new InteracaoCanal
        {
            IdClinica = tutor?.IdClinica,
            IdTutor = tutor?.Id,
            DsCanal = dto.DsCanal,
            DsDirecao = dto.DsDirecao,
            DsConteudo = conteudo,
            DtRecebimento = dto.DtRecebimento,
            DsMetadados = dto.DsMetadados?.GetRawText()
        };

        await _interacaoRepository.AddAsync(interacao);
        await _uow.CommitAsync();

        return new InteractionResponseDto { IdInteracao = interacao.Id };
    }

    public async Task<TriageResponseDto> RegistrarTriagemAsync(TriageRequestDto dto)
    {
        var interacao = await _interacaoRepository.GetByIdAsync(dto.IdInteracao)
            ?? throw new EntidadeNaoEncontradaException("InteracaoCanal", dto.IdInteracao);

        var tutor = await _tutorRepository.GetByIdAsync(dto.IdTutor)
            ?? throw new EntidadeNaoEncontradaException("Tutor", dto.IdTutor);

        // TASK-67 fix round 1 (Important-3 da revisão): sem JWT de clínica nestes 3
        // endpoints, IdClinicaFiltro é sempre null e o HasQueryFilter de
        // KuraDbContext.ApplyTenantFilters fica INERTE (não filtra nada) — as duas
        // leituras acima (GetByIdAsync por PK) não escopam por clínica sozinhas. Sem
        // esta checagem, uma triagem gravada com ID_CLINICA da clínica do tutor podia
        // carregar ID_INTERACAO apontando para uma interação de OUTRA clínica —
        // inconsistência de FK cross-tenant.
        //
        // ATUALIZAÇÃO (LU-08): o join que este comentário previa ("vira vazamento
        // real no dia em que alguém adicionar esse join") agora EXISTE —
        // GET /api/v1/luna/triagens (LunaController.ListarTriagens →
        // LunaService.ListarTriagensAsync) lê INTERACAO_CANAL a partir de
        // TRIAGEM_LUNA para compor trechoMensagem. O predicado de clínica desse join
        // NÃO é delegado ao HasQueryFilter global — está explícito na chave composta
        // (IdInteracao, IdClinica) do join em
        // TriagemLunaRepository.ListarPorClinicaAsync, exatamente para que uma
        // inconsistência como a que esta checagem previne (se algum dia escapar por
        // um caminho de escrita futuro que não passe por aqui) degrade para
        // trechoMensagem=null em vez de vazar DS_CONTEUDO de outra clínica. A checagem
        // abaixo continua sendo a defesa PRIMÁRIA (impede a inconsistência de nascer);
        // o predicado do join é a defesa SECUNDÁRIA (contém o dano se ela nascer
        // mesmo assim). Mensagem sem PII de propósito.
        //
        // Decisão TASK-77 (FIX_7): InteracaoCanal.IdClinica é nullable desde esta task
        // (interação de tutor não identificado grava com IdClinica null — ver
        // RegistrarInteracaoAsync). NÃO afrouxar esta checagem para o caso null: uma
        // triagem SEMPRE tem id_tutor conhecido (TriageRequestDto.IdTutor é
        // obrigatório, não nullable — a Luna só triga depois de identificar o tutor).
        // Se a interação referenciada por id_interacao não tem clínica atribuída
        // (IdClinica null), associá-la a um tutor real é exatamente o tipo de
        // inconsistência que esta checagem existe para pegar — ex.: um id_interacao
        // "chutado"/reciclado de uma interação anônima anterior. Rejeitar com o mesmo
        // 422 do caso cross-tenant, sem criar um terceiro caminho silencioso. Coberto
        // por RegistrarTriagemAsync_InteracaoSemClinicaAtribuida_Lanca422 em
        // LunaServiceTests.
        if (interacao.IdClinica is null || interacao.IdClinica != tutor.IdClinica)
        {
            throw new RegraDeNegocioException(
                "id_interacao não pertence à clínica do tutor informado (id_tutor).");
        }

        var triagem = new TriagemLuna
        {
            IdClinica = tutor.IdClinica,
            IdTutor = tutor.Id,
            IdInteracao = interacao.Id,
            DsNivelUrgencia = dto.DsUrgencia,
            DsDescricao = ComporDescricao(dto.Sintomas, dto.NrScore, dto.DsRecomendacao),
            // Decisão 3 (TASK-67): DT_TRIAGEM é NOT NULL e não vem do payload — coalesce
            // no service para "agora" (mesmo padrão TASK-56/60: nunca NotEmpty() no
            // validator pra consertar shape de coluna que o cliente não popula).
            DtTriagem = DateTime.UtcNow,

            // LU-08 (V21, em paralelo): colunas estruturadas novas, além de
            // DS_DESCRICAO (que continua sendo composta acima — nenhum consumidor
            // antigo quebra).
            NrScore = dto.NrScore,
            DsSintomas = ComporSintomas(dto.Sintomas),
            DsRegrasVersao = dto.DsRegrasVersao,

            // D-L5 (LU-08, fecha A2): único lugar do sistema que decide
            // ST_ENCAMINHADO_VET. ALTA vira encaminhamento automático; MEDIA/BAIXA
            // não.
            StEncaminhadoVet = dto.DsUrgencia == "ALTA"
        };

        await _triagemRepository.AddAsync(triagem);
        await _uow.CommitAsync();

        return new TriageResponseDto { IdTriagem = triagem.Id };
    }

    /// <summary>
    /// Decisão 2 (TASK-67): sintomas[]/nr_score/ds_recomendacao não têm coluna própria
    /// em TRIAGEM_LUNA (schema V9, anterior a esta feature). Composto em DS_DESCRICAO
    /// (VARCHAR2(2000)) em vez de pedir uma V16 — mantém os 3 endpoints desbloqueados
    /// nesta task sem tocar Flyway (que vive no backend-tutor-java). Ressalva no
    /// relatório: isso é *lossy* para consulta estruturada (ex.: filtrar triagens por
    /// nr_score) — recomendação é uma V16 futura com colunas próprias se esse tipo de
    /// consulta vier a ser necessário.
    /// </summary>
    private static string ComporDescricao(List<string> sintomas, int score, string recomendacao)
    {
        var sintomasTexto = sintomas.Count > 0 ? string.Join(", ", sintomas) : "não informado";
        var texto = $"Sintomas: {sintomasTexto}. Score: {score}. Recomendação: {recomendacao}";
        return TruncarPorBytesUtf8(texto, MaxTamanhoDescricaoTriagemBytes);
    }

    /// <summary>
    /// LU-08: grava sintomas[] em DS_SINTOMAS como texto delimitado por ';'
    /// (DelimitadorSintomas) — não JSON, não CSV, documentado em TriagemLuna.cs e lido
    /// de volta (Split) em TriagemLunaRepository.ListarPorClinicaAsync. Lista vazia
    /// grava null (nenhum sintoma informado é diferente de "não informado" — esse
    /// texto continua só em DS_DESCRICAO, que é para exibição, não para parsing).
    /// Truncamento por BYTES UTF-8, nunca por caractere — mesmo raciocínio das duas
    /// constantes irmãs.
    /// </summary>
    private static string? ComporSintomas(List<string> sintomas)
    {
        if (sintomas.Count == 0)
            return null;

        var texto = string.Join(DelimitadorSintomas, sintomas);
        return TruncarPorBytesUtf8(texto, MaxTamanhoSintomasBytes);
    }

    /// <summary>
    /// TASK-67 fix round 1 (Important-2 da revisão): trunca <paramref name="texto"/> para
    /// caber em <paramref name="maxBytes"/> bytes UTF-8 — não em caracteres. Itera por
    /// <see cref="Rune"/> (unidade de escalar Unicode completa: nunca quebra um par
    /// substituto/emoji ao meio) somando o comprimento UTF-8 de cada um
    /// (<see cref="Rune.Utf8SequenceLength"/>) até que o próximo não caiba mais. Quando
    /// trunca de fato, reserva espaço para <see cref="MarcadorTruncamento"/> (Minor-5 da
    /// revisão: sem marcador, quem lê a linha depois não distingue "mensagem curta" de
    /// "mensagem cortada") — o orçamento de bytes é sempre respeitado, marcador incluso.
    /// </summary>
    internal static string TruncarPorBytesUtf8(string texto, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(texto) <= maxBytes)
            return texto;

        var marcadorBytes = Encoding.UTF8.GetByteCount(MarcadorTruncamento);
        var orcamentoTexto = Math.Max(0, maxBytes - marcadorBytes);

        var builder = new StringBuilder();
        var bytesUsados = 0;
        foreach (var rune in texto.EnumerateRunes())
        {
            var bytesRune = rune.Utf8SequenceLength;
            if (bytesUsados + bytesRune > orcamentoTexto)
                break;

            builder.Append(rune.ToString());
            bytesUsados += bytesRune;
        }

        builder.Append(MarcadorTruncamento);
        return builder.ToString();
    }
}
