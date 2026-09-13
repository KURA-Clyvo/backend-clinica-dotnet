namespace Kura.Application.Tests;

using System.Text.Json;
using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Luna;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

public class LunaServiceTests
{
    private readonly Mock<ITriagemLunaRepository> _triagemRepoMock = new();
    private readonly Mock<IRepository<InteracaoCanal>> _interacaoRepoMock = new();
    private readonly Mock<ITutorRepository> _tutorRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    // LU-08: usado só por ListarTriagensAsync (GET /luna/triagens, o único dos 4
    // endpoints deste service que tem JWT de clínica). Setup default devolve a
    // clínica semeada 1 — testes de ListarTriagensAsync sobrescrevem quando
    // precisam de outro valor.
    private readonly Mock<IClinicaContext> _clinicaContextMock = new();
    private readonly LunaService _sut;

    public LunaServiceTests()
    {
        _clinicaContextMock.Setup(c => c.IdClinica).Returns(1);

        _sut = new LunaService(
            _triagemRepoMock.Object,
            _interacaoRepoMock.Object,
            _tutorRepoMock.Object,
            _uowMock.Object,
            _clinicaContextMock.Object);
    }

    private static DateTime Inicio => new(2026, 5, 1);
    private static DateTime Fim => new(2026, 5, 31);

    // ── GerarRelatorioAsync (pré-existente, TASK-05) ────────────────────────

    [Fact]
    public async Task GerarRelatorioAsync_IntervaloValido_RetornaAgregacaoCorreta()
    {
        // Arrange
        var triagens = new List<TriagemLuna>
        {
            new() { Id = 1, IdClinica = 1, DsNivelUrgencia = "URGENTE", StEncaminhadoVet = true, DtTriagem = Inicio.AddDays(1), StAtiva = true, DsDescricao = "desc" },
            new() { Id = 2, IdClinica = 1, DsNivelUrgencia = "URGENTE", StEncaminhadoVet = true, DtTriagem = Inicio.AddDays(2), StAtiva = true, DsDescricao = "desc" },
            new() { Id = 3, IdClinica = 1, DsNivelUrgencia = "LEVE", StEncaminhadoVet = false, DtTriagem = Inicio.AddDays(3), StAtiva = true, DsDescricao = "desc" },
        };

        _triagemRepoMock.Setup(r => r.GetByIntervaloAsync(Inicio, Fim))
            .ReturnsAsync(triagens);

        // Act
        var result = await _sut.GerarRelatorioAsync(Inicio, Fim);

        // Assert
        result.Should().NotBeNull();
        result.TotalTriagens.Should().Be(3);
        result.EncaminhadasParaVet.Should().Be(2);
        result.PorUrgencia.Should().ContainKey("URGENTE").WhoseValue.Should().Be(2);
        result.PorUrgencia.Should().ContainKey("LEVE").WhoseValue.Should().Be(1);
        result.DataInicio.Should().Be(Inicio);
        result.DataFim.Should().Be(Fim);
    }

    [Fact]
    public async Task GerarRelatorioAsync_SemTriagensNoPeriodo_RetornaZeros()
    {
        // Arrange
        _triagemRepoMock.Setup(r => r.GetByIntervaloAsync(Inicio, Fim))
            .ReturnsAsync(new List<TriagemLuna>());

        // Act
        var result = await _sut.GerarRelatorioAsync(Inicio, Fim);

        // Assert
        result.TotalTriagens.Should().Be(0);
        result.EncaminhadasParaVet.Should().Be(0);
        result.PorUrgencia.Should().BeEmpty();
    }

    [Fact]
    public async Task GerarRelatorioAsync_DataFimAnteriorDataInicio_LancaRegraDeNegocio()
    {
        // Act
        var act = async () => await _sut.GerarRelatorioAsync(Fim, Inicio);

        // Assert
        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("DataFim não pode ser anterior à DataInicio.");
    }

    [Fact]
    public async Task GerarRelatorioAsync_IntervaloMaiorQue90Dias_LancaRegraDeNegocio()
    {
        // Arrange
        var inicio = new DateTime(2026, 1, 1);
        var fimFora = inicio.AddDays(91);

        // Act
        var act = async () => await _sut.GerarRelatorioAsync(inicio, fimFora);

        // Assert
        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().Be("Intervalo máximo de 90 dias.");
    }

    // ── RegistrarInteracaoAsync (TASK-67) ───────────────────────────────────

    private static Tutor TutorClinica42(long id = 7) => new()
    {
        Id = id,
        IdClinica = 42,
        NmTutor = "Fulano",
        NrCpf = "11122233344",
        DsEmail = "fulano@teste.com",
        NrTelefone = "5511999990000",
        StAtiva = true
    };

    [Fact]
    public async Task RegistrarInteracaoAsync_IdTutorNull_GravaComIdClinicaEIdTutorNulos()
    {
        // TASK-77 (FIX_7) — decisão de produto do Felipe, substitui o teste homônimo da
        // TASK-67 que provava um 422. Comportamento antigo: id_tutor null (telefone não
        // cadastrado) rejeitava a interação inteira e ainda gerava um erro FALSO em
        // LOG_ERRO do lado da Luna. Comportamento novo: grava mesmo assim, com
        // IdClinica/IdTutor nulos — ganho de auditoria, não de visibilidade (uma linha
        // com IdClinica nulo fica invisível a qualquer leitura escopada por clínica, ver
        // KuraDbContext.ApplyTenantFilters).
        //
        // Teste que morde: rodado contra o código da TASK-67 (HEAD 823f400, antes desta
        // task), este teste FALHA — o service lança RegraDeNegocioException e nunca
        // chama AddAsync/CommitAsync. Saída real colada no relatório da TASK-77.
        var dto = new InteractionRequestDto
        {
            IdTutor = null,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "MARCADOR_LGPD_conteudo_sensivel_x7f2",
            DtRecebimento = DateTime.UtcNow
        };

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        // Act
        var act = async () => await _sut.RegistrarInteracaoAsync(dto);
        // Assert
        await act.Should().NotThrowAsync();

        capturada.Should().NotBeNull();
        capturada!.IdClinica.Should().BeNull("tutor não identificado — não há como derivar a clínica");
        capturada.IdTutor.Should().BeNull();
        capturada.DsConteudo.Should().Be("MARCADOR_LGPD_conteudo_sensivel_x7f2");
        _tutorRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<long>()), Times.Never,
            "sem id_tutor no payload não há PK para buscar — não deve nem tentar");
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_IdTutorInexistente_Lanca404()
    {
        // Arrange
        _tutorRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Tutor?)null);

        var dto = new InteractionRequestDto
        {
            IdTutor = 99,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "oi",
            DtRecebimento = DateTime.UtcNow
        };

        // Act
        var act = async () => await _sut.RegistrarInteracaoAsync(dto);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_TutorValido_DerivaIdClinicaDoTutor()
    {
        // Arrange
        var tutor = TutorClinica42();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        var dto = new InteractionRequestDto
        {
            IdTutor = tutor.Id,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "Meu pet está com febre",
            DtRecebimento = new DateTime(2026, 8, 8, 10, 0, 0, DateTimeKind.Utc)
        };

        // Act
        await _sut.RegistrarInteracaoAsync(dto);

        // Assert
        capturada.Should().NotBeNull();
        capturada!.IdClinica.Should().Be(42, "ID_CLINICA é NOT NULL e a Luna nunca envia — só dá pra derivar do tutor");
        capturada.IdTutor.Should().Be(tutor.Id);
        capturada.DsConteudo.Should().Be("Meu pet está com febre");
        capturada.DsMetadados.Should().BeNull();
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_ConteudoMaiorQue4000_TruncaComMarcador()
    {
        // Arrange
        // DS_CONTEUDO é VARCHAR2(4000) NOT NULL — sem truncar, o Oracle real
        // estouraria (na verdade Oracle trunca com erro ORA-12899, "value too large
        // for column"), não silenciosamente. Truncar aqui evita o 500 completamente.
        // Conteúdo só-ASCII: 1 char = 1 byte, então este teste NÃO cobre o Important-2
        // (byte vs char) — ver RegistrarInteracaoAsync_ConteudoAcentuadoMaiorQue4000Bytes_TruncaPorBytesNaoPorCaracteres
        // logo abaixo para o caso que de fato distingue os dois.
        var tutor = TutorClinica42();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        var conteudoGigante = new string('a', 5000);
        var dto = new InteractionRequestDto
        {
            IdTutor = tutor.Id,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = conteudoGigante,
            DtRecebimento = DateTime.UtcNow
        };

        // Act
        await _sut.RegistrarInteracaoAsync(dto);

        // Assert
        System.Text.Encoding.UTF8.GetByteCount(capturada!.DsConteudo).Should().BeLessThanOrEqualTo(4000);
        capturada.DsConteudo.Should().EndWith("…[truncado]",
            "Minor-5 da revisão: quem lê a linha depois precisa distinguir mensagem " +
            "curta de mensagem cortada");
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_ConteudoAcentuadoMaiorQue4000Bytes_TruncaPorBytesNaoPorCaracteres()
    {
        // Arrange
        // TASK-67 fix round 1 — Important-2 da revisão, teste que morde: revertendo
        // TruncarPorBytesUtf8 para um truncamento por CARACTERE (dto.DsConteudo[..4000]),
        // este teste falha (o texto acentuado gerado tem exatamente 4000 caracteres mas
        // ~5600+ bytes em UTF-8 — muito acima do teto real da coluna
        // VARCHAR2(4000) BYTE). Com o fix (truncar por Rune, medindo bytes UTF-8 de
        // verdade), o resultado cabe sempre dentro de 4000 bytes.
        var tutor = TutorClinica42();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        // "não é possível avaliação " tem acentos e cedilhas — cada um custa 2 bytes em
        // UTF-8. Repetido até passar de 4000 caracteres (WhatsApp aceita até 4096).
        var trechoAcentuado = "não é possível avaliação sem informação adicional çãêôáíóú ";
        var conteudoAcentuado = string.Concat(Enumerable.Repeat(trechoAcentuado, 100))[..4000];

        var dto = new InteractionRequestDto
        {
            IdTutor = tutor.Id,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = conteudoAcentuado,
            DtRecebimento = DateTime.UtcNow
        };

        // Act
        await _sut.RegistrarInteracaoAsync(dto);

        // Assert
        var bytesGravados = System.Text.Encoding.UTF8.GetByteCount(capturada!.DsConteudo);
        bytesGravados.Should().BeLessThanOrEqualTo(4000,
            "VARCHAR2(4000) sem CHAR herda NLS_LENGTH_SEMANTICS=BYTE (default Oracle) — " +
            "gravar mais que 4000 bytes estoura ORA-12899 (500) contra Oracle real, " +
            "mesmo que o C# ache que 'só' são 4000 caracteres");
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_ConteudoComEmojiNoLimite_NaoQuebraParDeSurrogate()
    {
        // Arrange
        // Emoji custa 4 bytes UTF-8 e é representado por um par substituto (2 code
        // units) em C#. Um truncamento ingênuo por índice de char podia cortar bem no
        // meio do par, produzindo uma string malformada. TruncarPorBytesUtf8 itera por
        // Rune (unidade Unicode completa), então isso nunca acontece.
        var tutor = TutorClinica42();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        // 3999 'a' (1 byte cada) + uma sequência de emojis (4 bytes cada) — o corte cai
        // exatamente na fronteira de um emoji.
        var conteudoComEmoji = new string('a', 3999) + string.Concat(Enumerable.Repeat("🐾", 50));

        var dto = new InteractionRequestDto
        {
            IdTutor = tutor.Id,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = conteudoComEmoji,
            DtRecebimento = DateTime.UtcNow
        };

        // Act
        await _sut.RegistrarInteracaoAsync(dto);

        // Assert
        // String.IsNormalized não detecta par quebrado de forma confiável — a prova
        // real é: reencodar para UTF-8 e decodificar de volta não pode lançar nem
        // produzir caractere de substituição (U+FFFD), o que aconteceria com um
        // surrogate órfão.
        var bytes = System.Text.Encoding.UTF8.GetBytes(capturada!.DsConteudo);
        var textoRoundTrip = System.Text.Encoding.UTF8.GetString(bytes);
        textoRoundTrip.Should().NotContain("�", "um par substituto quebrado vira U+FFFD no round-trip UTF-8");
        System.Text.Encoding.UTF8.GetByteCount(capturada.DsConteudo).Should().BeLessThanOrEqualTo(4000);
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_ComMetadados_SerializaJsonBrutoNoClob()
    {
        // Arrange
        var tutor = TutorClinica42();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);

        InteracaoCanal? capturada = null;
        _interacaoRepoMock
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        using var doc = JsonDocument.Parse("""{"media_id":"abc123"}""");
        var dto = new InteractionRequestDto
        {
            IdTutor = tutor.Id,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = "oi",
            DtRecebimento = DateTime.UtcNow,
            DsMetadados = doc.RootElement.Clone()
        };

        // Act
        await _sut.RegistrarInteracaoAsync(dto);

        // Assert
        capturada!.DsMetadados.Should().Be("""{"media_id":"abc123"}""");
    }

    // ── RegistrarTriagemAsync (TASK-67) ─────────────────────────────────────

    private static InteracaoCanal InteracaoExistente(long id = 100, long idClinica = 42) => new()
    {
        Id = id,
        IdClinica = idClinica,
        IdTutor = 7,
        DsCanal = "WHATSAPP",
        DsDirecao = "INBOUND",
        DsConteudo = "oi",
        DtRecebimento = DateTime.UtcNow,
        StAtiva = true
    };

    [Fact]
    public async Task RegistrarTriagemAsync_PayloadRealDaLuna_NaoLancaEComponeDescricaoSemPerderDados()
    {
        // Teste que morde: TriageRequestDTO real da Luna manda sintomas[]/nr_score/
        // ds_recomendacao (sem coluna própria) e NÃO manda DS_DESCRICAO nem DT_TRIAGEM
        // (NOT NULL em TRIAGEM_LUNA, V9). Um mapeamento ingênuo (TriagemLuna { DsDescricao
        // = dto.DsDescricao }) nem compila — a versão que só ignora sintomas/score/
        // recomendacao perderia dado sem gravar em lugar nenhum. Este teste prova as
        // duas coisas: não lança, E os 3 campos aparecem em algum lugar da linha gravada.
        var tutor = TutorClinica42();
        var interacao = InteracaoExistente();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacao.Id)).ReturnsAsync(interacao);

        TriagemLuna? capturada = null;
        _triagemRepoMock
            .Setup(r => r.AddAsync(It.IsAny<TriagemLuna>()))
            .Callback<TriagemLuna>(t => capturada = t)
            .Returns(Task.CompletedTask);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacao.Id,
            IdTutor = tutor.Id,
            Sintomas = ["vomito", "letargia"],
            DsUrgencia = "ALTA",
            NrScore = 87,
            DsRecomendacao = "Levar ao veterinário em até 2 horas"
        };

        // Act
        var act = async () => await _sut.RegistrarTriagemAsync(dto);
        // Assert
        await act.Should().NotThrowAsync();

        capturada.Should().NotBeNull();
        capturada!.DsDescricao.Should().NotBeNullOrEmpty("DS_DESCRICAO é NOT NULL em TRIAGEM_LUNA");
        capturada.DsDescricao.Should().Contain("vomito").And.Contain("letargia").And.Contain("87").And.Contain("Levar ao veterinário");
        capturada.DsDescricao.Length.Should().BeLessThanOrEqualTo(2000, "DS_DESCRICAO é VARCHAR2(2000)");
        capturada.DtTriagem.Should().NotBe(default(DateTime), "DT_TRIAGEM é NOT NULL e não vem do payload — precisa de coalesce no service");
        capturada.IdClinica.Should().Be(42, "derivado do tutor, mesmo padrão de RegistrarInteracaoAsync");
        capturada.IdInteracao.Should().Be(interacao.Id);
        capturada.DsNivelUrgencia.Should().Be("ALTA");
    }

    [Fact]
    public async Task RegistrarTriagemAsync_DescricaoMaiorQue2000_Trunca()
    {
        // Arrange
        var tutor = TutorClinica42();
        var interacao = InteracaoExistente();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacao.Id)).ReturnsAsync(interacao);

        TriagemLuna? capturada = null;
        _triagemRepoMock
            .Setup(r => r.AddAsync(It.IsAny<TriagemLuna>()))
            .Callback<TriagemLuna>(t => capturada = t)
            .Returns(Task.CompletedTask);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacao.Id,
            IdTutor = tutor.Id,
            Sintomas = Enumerable.Range(0, 400).Select(i => $"sintoma{i}").ToList(),
            DsUrgencia = "BAIXA",
            NrScore = 10,
            DsRecomendacao = new string('x', 3000)
        };

        // Act
        await _sut.RegistrarTriagemAsync(dto);

        // Assert
        System.Text.Encoding.UTF8.GetByteCount(capturada!.DsDescricao).Should().BeLessThanOrEqualTo(2000);
        capturada.DsDescricao.Should().EndWith("…[truncado]");
    }

    [Fact]
    public async Task RegistrarTriagemAsync_DescricaoAcentuadaMaiorQue2000Bytes_TruncaPorBytesNaoPorCaracteres()
    {
        // Arrange
        // Mesmo bug de Important-2, no outro campo que passa pelo mesmo helper
        // (TruncarPorBytesUtf8) — DS_DESCRICAO é VARCHAR2(2000) BYTE.
        var tutor = TutorClinica42();
        var interacao = InteracaoExistente();
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacao.Id)).ReturnsAsync(interacao);

        TriagemLuna? capturada = null;
        _triagemRepoMock
            .Setup(r => r.AddAsync(It.IsAny<TriagemLuna>()))
            .Callback<TriagemLuna>(t => capturada = t)
            .Returns(Task.CompletedTask);

        var recomendacaoAcentuada = string.Concat(Enumerable.Repeat(
            "recomendação médica não é possível sem avaliação presencial çãêôáíóú ", 60));

        var dto = new TriageRequestDto
        {
            IdInteracao = interacao.Id,
            IdTutor = tutor.Id,
            Sintomas = ["vomito"],
            DsUrgencia = "ALTA",
            NrScore = 90,
            DsRecomendacao = recomendacaoAcentuada
        };

        // Act
        await _sut.RegistrarTriagemAsync(dto);

        // Assert
        System.Text.Encoding.UTF8.GetByteCount(capturada!.DsDescricao).Should().BeLessThanOrEqualTo(2000,
            "DS_DESCRICAO é VARCHAR2(2000) BYTE — o mesmo raciocínio do Important-2 " +
            "se aplica aqui, não só em DS_CONTEUDO");
    }

    [Fact]
    public async Task RegistrarTriagemAsync_InteracaoDeOutraClinica_Lanca422ENaoGrava()
    {
        // Arrange
        // TASK-67 fix round 1 — Important-3 da revisão, teste que morde: sem a
        // checagem `interacao.IdClinica != tutor.IdClinica`, este teste passaria uma
        // triagem gravável com FK cruzando clínicas (interação da clínica 99 associada
        // a um tutor da clínica 42). Nas condições reais destes 3 endpoints (API Key,
        // sem JWT), o query filter de tenant fica inerte — ver
        // InteracaoCanalTenantIsolationTests — então esta checagem explícita é a única
        // defesa real contra essa inconsistência cross-tenant.
        var tutor = TutorClinica42(); // IdClinica = 42
        var interacaoDeOutraClinica = InteracaoExistente(id: 200, idClinica: 99);
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacaoDeOutraClinica.Id)).ReturnsAsync(interacaoDeOutraClinica);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacaoDeOutraClinica.Id,
            IdTutor = tutor.Id,
            Sintomas = ["vomito"],
            DsUrgencia = "ALTA",
            NrScore = 90,
            DsRecomendacao = "levar ao vet"
        };

        // Act
        var act = async () => await _sut.RegistrarTriagemAsync(dto);

        // Assert
        var ex = await act.Should().ThrowAsync<RegraDeNegocioException>();
        ex.Which.Message.Should().NotContain("42").And.NotContain("99",
            "mensagem sem PII/detalhe interno de propósito — só que a combinação é inválida");
        _triagemRepoMock.Verify(r => r.AddAsync(It.IsAny<TriagemLuna>()), Times.Never,
            "a triagem cross-tenant não pode chegar a ser gravada");
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task RegistrarTriagemAsync_InteracaoSemClinicaAtribuida_Lanca422()
    {
        // Arrange
        // TASK-77 (FIX_7): interação gravada com IdClinica null (tutor não identificado
        // no momento da mensagem, ver RegistrarInteracaoAsync) referenciada depois por
        // uma triagem que TEM tutor conhecido (TriageRequestDto.IdTutor não é nullable).
        // Decisão documentada em RegistrarTriagemAsync: não afrouxar a checagem
        // cross-tenant para esse caso — uma triagem sempre tem tutor identificado, então
        // associá-la a uma interação sem clínica atribuída é o tipo de inconsistência
        // que a checagem já existe para pegar (ex.: id_interacao reciclado/errado).
        // Teste que morde: se a checagem virasse `interacao.IdClinica != tutor.IdClinica`
        // sem o `is null ||` explícito, o comportamento AINDA seria correto por
        // igualdade lifted do C# (null != 5 → true) — mas essa é exatamente a trivia de
        // linguagem que este teste existe para não depender de leitura de código, e sim
        // de comportamento provado.
        var tutor = TutorClinica42();
        var interacaoSemClinica = InteracaoExistente(id: 300);
        interacaoSemClinica.IdClinica = null;
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacaoSemClinica.Id)).ReturnsAsync(interacaoSemClinica);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacaoSemClinica.Id,
            IdTutor = tutor.Id,
            Sintomas = ["vomito"],
            DsUrgencia = "ALTA",
            NrScore = 90,
            DsRecomendacao = "levar ao vet"
        };

        // Act
        var act = async () => await _sut.RegistrarTriagemAsync(dto);

        // Assert
        await act.Should().ThrowAsync<RegraDeNegocioException>();
        _triagemRepoMock.Verify(r => r.AddAsync(It.IsAny<TriagemLuna>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task RegistrarTriagemAsync_InteracaoDaMesmaClinica_NaoLanca()
    {
        // Arrange
        // Contraparte "caminho feliz" do teste acima — prova que a checagem nova não
        // é falso-positivo pro caso normal (interação e tutor da mesma clínica).
        var tutor = TutorClinica42();
        var interacao = InteracaoExistente(idClinica: 42);
        _tutorRepoMock.Setup(r => r.GetByIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacao.Id)).ReturnsAsync(interacao);
        _triagemRepoMock.Setup(r => r.AddAsync(It.IsAny<TriagemLuna>())).Returns(Task.CompletedTask);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacao.Id,
            IdTutor = tutor.Id,
            Sintomas = ["vomito"],
            DsUrgencia = "ALTA",
            NrScore = 90,
            DsRecomendacao = "levar ao vet"
        };

        // Act
        var act = async () => await _sut.RegistrarTriagemAsync(dto);

        // Assert
        await act.Should().NotThrowAsync();
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task RegistrarTriagemAsync_InteracaoInexistente_Lanca404()
    {
        // Arrange
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((InteracaoCanal?)null);

        var dto = new TriageRequestDto
        {
            IdInteracao = 999,
            IdTutor = 7,
            Sintomas = ["tosse"],
            DsUrgencia = "BAIXA",
            NrScore = 5,
            DsRecomendacao = "observar"
        };

        // Act
        var act = async () => await _sut.RegistrarTriagemAsync(dto);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>(
            "gravar TriagemLuna.IdInteracao apontando pra uma interação inexistente " +
            "estouraria a FK do Oracle (ORA-02291) se não fosse checado antes — 500");
    }

    [Fact]
    public async Task RegistrarTriagemAsync_TutorInexistente_Lanca404()
    {
        // Arrange
        var interacao = InteracaoExistente();
        _interacaoRepoMock.Setup(r => r.GetByIdAsync(interacao.Id)).ReturnsAsync(interacao);
        _tutorRepoMock.Setup(r => r.GetByIdAsync(555)).ReturnsAsync((Tutor?)null);

        var dto = new TriageRequestDto
        {
            IdInteracao = interacao.Id,
            IdTutor = 555,
            Sintomas = ["tosse"],
            DsUrgencia = "BAIXA",
            NrScore = 5,
            DsRecomendacao = "observar"
        };

        // Act
        var act = async () => await _sut.RegistrarTriagemAsync(dto);

        // Assert
        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }
}
