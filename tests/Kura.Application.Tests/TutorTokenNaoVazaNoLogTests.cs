namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Tutor;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

/// <summary>
/// REC-01 (KURA_BACKLOG_RECEPCAO.md): mordida (d) do aceite — "o token não aparece no log".
/// O token do invite é credencial de portador (quem tiver o link cria a conta do tutor), então
/// nunca pode chegar a um log de aplicação.
///
/// Diferente de um mock que só verificaria "Log foi chamado" (que não prova nada sobre o TEXTO
/// final), este teste usa um <see cref="ILoggerProvider"/> capturador real, plugado num
/// <see cref="ILoggerFactory"/> de verdade — captura a mensagem FORMATADA de QUALQUER logger
/// criado através dele. <c>Convite:UrlBaseAppTutor</c> é CONFIGURADA de propósito (não ausente):
/// só assim <see cref="GeradorLinkConvite.GerarLink"/> passa da checagem de base nula e executa
/// o resto do método — é ali, não no construtor, que uma mutação futura poderia logar o token.
///
/// Controle positivo (regra de ouro deste ecossistema — "0 não é prova de ausência sem provar
/// que o instrumento veria 1"): loga um valor de propósito DEPOIS do exercício real e confirma
/// que o MESMO mecanismo de busca o encontra — se essa asserção falhasse, o "não encontrado" do
/// token seria inconclusivo, não uma prova.
/// </summary>
public class TutorTokenNaoVazaNoLogTests
{
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> MensagensFormatadas { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(MensagensFormatadas);

        public void Dispose() { }

        private sealed class CapturingLogger(List<string> sink) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                sink.Add(formatter(state, exception));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }

    /// <summary>
    /// <see cref="IConfiguration"/> mínima só com o que <see cref="GeradorLinkConvite"/>
    /// realmente lê (o indexador) — evita depender do pacote concreto
    /// <c>Microsoft.Extensions.Configuration</c> (ConfigurationBuilder/AddInMemoryCollection)
    /// só para este teste.
    /// </summary>
    private sealed class ConfiguracaoFake(Dictionary<string, string?> valores) : IConfiguration
    {
        public string? this[string key]
        {
            get => valores.TryGetValue(key, out var v) ? v : null;
            set => valores[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => throw new NotSupportedException("não usado por GeradorLinkConvite");
        public IConfigurationSection GetSection(string key) => throw new NotSupportedException("não usado por GeradorLinkConvite");
    }

    [Fact]
    public async Task CreateAsync_TokenDoInviteNuncaApareceEmNenhumLogEmitido()
    {
        // Arrange — instrumento: um ILoggerFactory real com o provider capturador acima,
        // recebendo QUALQUER logger construído através dele.
        var provider = new CapturingLoggerProvider();
        using var loggerFactory = new LoggerFactory([provider]);

        // Convite:UrlBaseAppTutor CONFIGURADA de propósito: é só neste ramo que
        // GerarLink() passa da checagem de base nula e executa o resto do método — o ramo
        // ausente (WARN na partida, GerarLink devolve null antes de qualquer outra linha)
        // não exercitaria uma mutação escrita DENTRO de GerarLink. Testado em separado
        // (CreateAsync_GeradorLinkConviteDevolveNull_DsLinkConviteVaiNullNaResposta).
        var configuracao = new ConfiguracaoFake(new Dictionary<string, string?>
        {
            ["Convite:UrlBaseAppTutor"] = "https://tutor.exemplo.com"
        });
        var geradorLinkConvite = new GeradorLinkConvite(
            configuracao,
            loggerFactory.CreateLogger<GeradorLinkConvite>());

        var tutorRepoMock = new Mock<ITutorRepository>();
        tutorRepoMock.Setup(r => r.AddAsync(It.IsAny<Tutor>())).Returns(Task.CompletedTask);

        InviteTutor? inviteCapturado = null;
        var inviteRepoMock = new Mock<IInviteTutorRepository>();
        inviteRepoMock.Setup(r => r.AddAsync(It.IsAny<InviteTutor>()))
            .Callback<InviteTutor>(i => inviteCapturado = i)
            .Returns(Task.CompletedTask);

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var sut = new TutorService(
            tutorRepoMock.Object,
            Mock.Of<ITutorPetRepository>(),
            Mock.Of<IRepository<Especie>>(),
            Mock.Of<IRepository<Raca>>(),
            inviteRepoMock.Object,
            uowMock.Object,
            Mock.Of<IClinicaContext>(),
            Mock.Of<IGeradorUrlFotoPet>(),
            geradorLinkConvite);

        var dto = new TutorCreateDto
        {
            NmTutor = "Maria Silva",
            NrCpf = "12345678901",
            DsEmail = "maria@email.com",
            NrTelefone = "11999999999",
            StAvisoPrivacidadeInformado = true
        };

        // Act
        var resultado = await sut.CreateAsync(dto, 1L);

        inviteCapturado.Should().NotBeNull();
        var token = inviteCapturado!.NrToken.ToString();

        // Assert — CONTROLE POSITIVO primeiro: o MESMO instrumento (provider + Contains)
        // acha um valor logado DE PROPÓSITO agora. Sem isto, um "não encontrado" abaixo seria
        // inconclusivo (podia ser instrumento cego, não ausência real).
        var valorDeControle = Guid.NewGuid().ToString();
        loggerFactory.CreateLogger("ControlePositivo").LogWarning("valor de controle: {Valor}", valorDeControle);
        provider.MensagensFormatadas.Should().Contain(m => m.Contains(valorDeControle));

        // Assert real — mordida (d): logar o token de propósito (ex.: um LogInformation com o
        // NrToken em GeradorLinkConvite.GerarLink) faria este bloco falhar.
        provider.MensagensFormatadas.Should().NotContain(m => m.Contains(token));

        // Confere que GerarLink() de fato executou até o fim do método (base configurada,
        // link não-nulo na resposta) — senão este teste não estaria exercitando o corpo de
        // GerarLink, só o construtor, e a mordida (d) escrita ali passaria despercebida.
        resultado.DsLinkConvite.Should().NotBeNull();
        resultado.DsLinkConvite.Should().Contain(token);
    }
}
