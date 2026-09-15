namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Luna;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;

/// <summary>
/// TASK-67, ruling do maestro (Bloco 0 §B0.3): DS_CONTEUDO (mensagem do tutor) e o
/// telefone podem ser PERSISTIDOS na tabela (é o propósito dela), mas NUNCA podem
/// aparecer em log de aplicação, em LOG_ERRO, ou em mensagem de exceção — mesma classe
/// de vazamento que a TASK-46 corrigiu do lado da Luna (telefone cru em log).
///
/// CORREÇÃO (fix round 1, Important-1 da revisão cética): a versão anterior deste
/// comentário afirmava que "o middleware nunca lê campos do request diretamente" — isso
/// era **falso**. <c>ExceptionHandlerMiddleware</c> loga <c>context.Request.Path</c>
/// integralmente, e GET /api/v1/tutores/telefone/{numero} carrega o telefone **no
/// path**, não no body — logo o telefone vazava para o log em qualquer exceção nesse
/// endpoint. Corrigido em <c>ExceptionHandlerMiddleware.RedigirPathSensivel</c> e
/// provado de verdade (exercitando o middleware com um logger capturador, não por
/// leitura de código) em <c>ExceptionHandlerMiddlewareLgpdTests.cs</c>
/// (`tests/Kura.Infrastructure.Tests/`).
///
/// O que ESTA classe prova, com escopo mais estreito e honesto: nenhuma exceção
/// lançada pelo caminho novo (TASK-67) interpola <c>ds_conteudo</c> na sua
/// <c>Message</c> (o corpo RFC 7807 devolve <c>ex.Message</c> como <c>title</c>, e o
/// middleware também loga o texto da exceção — então a Message em si é sensível). Para
/// o telefone, o caminho de "não encontrado" em <c>BuscarContextoPorTelefoneAsync</c>
/// é modelado como retorno <c>null</c>, nunca exceção — não há Message nenhuma para
/// inspecionar nesse caso, o que já elimina esse vetor por construção.
/// </summary>
public class LgpdNaoVazamentoTests
{
    private const string ConteudoSensivel = "MARCADOR_LGPD_conteudo_de_mensagem_x9k2";
    private const string TelefoneSensivel = "5511900009999";

    [Fact]
    public async Task RegistrarInteracaoAsync_IdTutorNull_NaoLancaENaoVazaDsConteudoEmExcecao()
    {
        // Arrange
        // Reescrito na TASK-77 (FIX_7): id_tutor null deixou de lançar
        // RegraDeNegocioException — a interação passa a ser GRAVADA com IdClinica/IdTutor
        // nulos (decisão de produto, ver LunaService.RegistrarInteracaoAsync). O teste
        // original desta classe provava "a mensagem de exceção não contém ds_conteudo";
        // hoje o vetor é mais forte ainda: não há exceção nenhuma nesse caminho, então
        // não há Message para vazar por construção — mesmo raciocínio já usado abaixo
        // para BuscarContextoPorTelefoneAsync (ausência modelada sem exceção). O que
        // continua valendo é o contrato desta classe: ds_conteudo PODE ser persistido
        // (é o propósito da tabela), mas nunca deve aparecer fora dela por este caminho.
        var interacaoRepo = new Mock<IRepository<InteracaoCanal>>();
        InteracaoCanal? capturada = null;
        interacaoRepo
            .Setup(r => r.AddAsync(It.IsAny<InteracaoCanal>()))
            .Callback<InteracaoCanal>(i => capturada = i)
            .Returns(Task.CompletedTask);

        var sut = new LunaService(
            Mock.Of<ITriagemLunaRepository>(),
            interacaoRepo.Object,
            Mock.Of<ITutorRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IClinicaContext>());

        var dto = new InteractionRequestDto
        {
            IdTutor = null,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = ConteudoSensivel,
            DtRecebimento = DateTime.UtcNow
        };

        // Act
        var act = async () => await sut.RegistrarInteracaoAsync(dto);

        // Assert
        await act.Should().NotThrowAsync(
            "não há mais rejeição para id_tutor null — se este teste lançar, é regressão " +
            "da decisão de produto da TASK-77, não um comportamento LGPD válido");
        capturada.Should().NotBeNull();
        capturada!.DsConteudo.Should().Be(ConteudoSensivel,
            "ds_conteudo é persistido normalmente — o que este teste garante é que não " +
            "há exceção (logo não há Message) por onde ele pudesse vazar");
    }

    [Fact]
    public async Task RegistrarInteracaoAsync_TutorInexistente_MensagemDeExcecaoNaoContemDsConteudo()
    {
        // Arrange
        var tutorRepo = new Mock<ITutorRepository>();
        tutorRepo.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync((Tutor?)null);

        var sut = new LunaService(
            Mock.Of<ITriagemLunaRepository>(),
            Mock.Of<IRepository<InteracaoCanal>>(),
            tutorRepo.Object,
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IClinicaContext>());

        var dto = new InteractionRequestDto
        {
            IdTutor = 999,
            DsCanal = "WHATSAPP",
            DsDirecao = "INBOUND",
            DsConteudo = ConteudoSensivel,
            DtRecebimento = DateTime.UtcNow
        };

        // Act
        var act = async () => await sut.RegistrarInteracaoAsync(dto);

        // Assert
        var ex = await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
        ex.Which.Message.Should().NotContain(ConteudoSensivel);
    }

    [Fact]
    public async Task BuscarContextoPorTelefoneAsync_TutorNaoEncontrado_RetornaNullSemLancarNadaComOTelefone()
    {
        // Arrange
        var tutorRepo = new Mock<ITutorRepository>();
        tutorRepo.Setup(r => r.GetByTelefoneAsync(TelefoneSensivel)).ReturnsAsync((Tutor?)null);

        var sut = new TutorService(
            tutorRepo.Object,
            Mock.Of<ITutorPetRepository>(),
            Mock.Of<IRepository<Especie>>(),
            Mock.Of<IRepository<Raca>>(),
            Mock.Of<IInviteTutorRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IClinicaContext>());

        // Act
        // "Não encontrado" é modelado como null, nunca como exceção — a forma mais
        // segura possível de reportar ausência sem correr risco de interpolar o
        // telefone numa mensagem que sobe até o middleware/log.
        var result = await sut.BuscarContextoPorTelefoneAsync(TelefoneSensivel);

        // Assert
        result.Should().BeNull();
    }
}
