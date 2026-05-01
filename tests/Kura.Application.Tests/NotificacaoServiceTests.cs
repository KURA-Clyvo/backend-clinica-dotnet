namespace Kura.Application.Tests;

using FluentAssertions;
using Moq;
using Kura.Application.DTOs.Notificacao;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using System.Linq.Expressions;

public class NotificacaoServiceTests
{
    private readonly Mock<IRepository<Notificacao>> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IClinicaContext> _ctxMock = new();
    private readonly NotificacaoService _sut;

    public NotificacaoServiceTests()
    {
        _ctxMock.Setup(c => c.IdClinica).Returns(10L);
        _sut = new NotificacaoService(_repoMock.Object, _uowMock.Object, _ctxMock.Object);
    }

    [Fact]
    public async Task GetAllByClinicaAsync_RetornaNotificacoesClinica()
    {
        var clinicaId = 10L;
        var lista = new List<Notificacao>
        {
            new() { Id = 1, IdClinica = clinicaId, DsTitulo = "A", DsMensagem = "msg", StLida = 'N', StAtiva = 'S' },
            new() { Id = 2, IdClinica = clinicaId, DsTitulo = "B", DsMensagem = "msg", StLida = 'S', StAtiva = 'S' }
        };
        _repoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Notificacao, bool>>>()))
            .ReturnsAsync(lista);

        var result = await _sut.GetAllByClinicaAsync(clinicaId, null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllByClinicaAsync_ApenasNaoLidas_FiltraCorretamente()
    {
        var clinicaId = 10L;
        var lista = new List<Notificacao>
        {
            new() { Id = 1, IdClinica = clinicaId, DsTitulo = "A", DsMensagem = "msg", StLida = 'N', StAtiva = 'S' },
            new() { Id = 2, IdClinica = clinicaId, DsTitulo = "B", DsMensagem = "msg", StLida = 'S', StAtiva = 'S' }
        };
        _repoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Notificacao, bool>>>()))
            .ReturnsAsync(lista);

        var result = await _sut.GetAllByClinicaAsync(clinicaId, true);

        result.Should().HaveCount(1);
        result.First().StLida.Should().Be('N');
    }

    [Fact]
    public async Task MarcarLidaAsync_NotificacaoNaoExiste_LancaEntidadeNaoEncontrada()
    {
        _repoMock.Setup(r => r.GetByIdAsync(99L)).ReturnsAsync((Notificacao?)null);

        var act = async () => await _sut.MarcarLidaAsync(99L);

        await act.Should().ThrowAsync<EntidadeNaoEncontradaException>();
    }

    [Fact]
    public async Task MarcarLidaAsync_NotificacaoJaLida_LancaRegraDeNegocio()
    {
        var notificacao = new Notificacao { Id = 1, IdClinica = 10L, DsTitulo = "T", DsMensagem = "M", StLida = 'S', StAtiva = 'S' };
        _repoMock.Setup(r => r.GetByIdAsync(1L)).ReturnsAsync(notificacao);

        var act = async () => await _sut.MarcarLidaAsync(1L);

        await act.Should().ThrowAsync<RegraDeNegocioException>()
            .WithMessage("Notificação já foi lida.");
    }

    [Fact]
    public async Task MarcarLidaAsync_CaminhoFeliz_MarcaStLidaS()
    {
        var notificacao = new Notificacao { Id = 1, IdClinica = 10L, DsTitulo = "T", DsMensagem = "M", StLida = 'N', StAtiva = 'S' };
        _repoMock.Setup(r => r.GetByIdAsync(1L)).ReturnsAsync(notificacao);

        await _sut.MarcarLidaAsync(1L);

        notificacao.StLida.Should().Be('S');
        notificacao.DtLeitura.Should().NotBeNull();
        _repoMock.Verify(r => r.Update(notificacao), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }
}
