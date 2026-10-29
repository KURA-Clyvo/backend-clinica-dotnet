namespace Kura.Application.Tests;

using FluentAssertions;
using Kura.Application.Services;
using Kura.Domain.Entities;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Domain.Paginacao;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// S4-NET-04 — <c>AgendaService.ListarPaginadoAsync</c> (lista da v2) e os predicados estáticos que
/// decidem as ações HAL. A regra de transição NÃO é duplicada: estes testes só provam que a v2 lê o
/// mesmo mapa que o <c>AtualizarStatusAsync</c>.
/// </summary>
public class AgendaServiceListarPaginadoTests
{
    private readonly Mock<IAgendamentoReadRepository> _readRepo = new();
    private readonly Mock<IClinicaContext> _clinica = new();
    private readonly AgendaService _sut;

    public AgendaServiceListarPaginadoTests()
    {
        _clinica.Setup(c => c.IdClinica).Returns(7L);
        _sut = new AgendaService(
            _readRepo.Object, _clinica.Object, new Mock<IAgendamentoRepository>().Object,
            new Mock<IUnitOfWork>().Object, new Mock<IGeradorUrlFotoPet>().Object,
            new Mock<ILogger<AgendaService>>().Object, new Mock<ITutorRepository>().Object,
            new Mock<IPetRepository>().Object, new Mock<IVeterinarioRepository>().Object,
            new Mock<ITriagemLunaRepository>().Object, new Mock<IRelogioClinica>().Object);
    }

    [Fact]
    public async Task ListarPaginadoAsync_UsaAClinicaDoContexto_EDevolveTotalDoRepositorio()
    {
        // Arrange
        var consulta = new ConsultaAgendamentos(new DateTime(2026, 10, 26), new DateTime(2026, 10, 28), 2, 5);
        _readRepo.Setup(r => r.ListarPaginadoAsync(7L, consulta))
            .ReturnsAsync(new PaginaDe<Agendamento>(
                [new Agendamento { Id = 1, IdClinica = 7, DtAgendamento = new DateTime(2026, 10, 26, 9, 0, 0), StStatus = "AGENDADO" }], 2, 5, 11));

        // Act
        var pagina = await _sut.ListarPaginadoAsync(consulta);

        // Assert
        pagina.Total.Should().Be(11);
        pagina.Pagina.Should().Be(2);
        pagina.Itens.Single().DsEtapaRecepcao.Should().Be("AGENDADO");
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(32, false)]
    public async Task ListarPaginadoAsync_LimiteDe31Dias_IgualAV1(int dias, bool aceita)
    {
        var inicio = new DateTime(2026, 10, 1);
        var consulta = new ConsultaAgendamentos(inicio, inicio.AddDays(dias));
        _readRepo.Setup(r => r.ListarPaginadoAsync(7L, consulta))
            .ReturnsAsync(new PaginaDe<Agendamento>([], 1, 20, 0));

        var act = () => _sut.ListarPaginadoAsync(consulta);

        if (aceita) await act.Should().NotThrowAsync();
        else await act.Should().ThrowAsync<RegraDeNegocioException>().WithMessage("*31 dias*");
    }

    [Fact]
    public async Task ListarPaginadoAsync_FimAnteriorAoInicio_Recusa()
    {
        var act = () => _sut.ListarPaginadoAsync(
            new ConsultaAgendamentos(new DateTime(2026, 10, 10), new DateTime(2026, 10, 9)));

        await act.Should().ThrowAsync<RegraDeNegocioException>().WithMessage("*anterior*");
    }

    [Theory]
    [InlineData("INTENCAO", true)]
    [InlineData("AGENDADO", true)]
    [InlineData("CONFIRMADO", true)]
    [InlineData("REALIZADO", false)]
    [InlineData("CANCELADO", false)]
    [InlineData("NAO_COMPARECEU", false)]
    [InlineData(null, false)]
    [InlineData("INVENTADO", false)]
    public void AdmiteTransicaoDeStatus_SegueOMapaDaMaquinaDeEstados(string? status, bool esperado) =>
        AgendaService.AdmiteTransicaoDeStatus(status).Should().Be(esperado);

    [Fact]
    public void AdmiteCheckinEInicio_SoParaStatusElegivelEEstadoPendente()
    {
        var agora = new DateTime(2026, 10, 26, 9, 0, 0);

        AgendaService.AdmiteCheckin("AGENDADO", null, null).Should().BeTrue();
        AgendaService.AdmiteCheckin("CONFIRMADO", null, null).Should().BeTrue();
        AgendaService.AdmiteCheckin("AGENDADO", agora, null).Should().BeFalse("já fez check-in");
        AgendaService.AdmiteCheckin("AGENDADO", null, agora).Should().BeFalse("início sem check-in bloqueia check-in tardio");
        AgendaService.AdmiteCheckin("CANCELADO", null, null).Should().BeFalse();
        AgendaService.AdmiteCheckin("INTENCAO", null, null).Should().BeFalse();

        AgendaService.AdmiteInicioAtendimento("AGENDADO", null).Should().BeTrue();
        AgendaService.AdmiteInicioAtendimento("AGENDADO", agora).Should().BeFalse("já iniciado");
        AgendaService.AdmiteInicioAtendimento("REALIZADO", null).Should().BeFalse();
    }

    [Fact]
    public void StatusConhecidos_SaoOsSeisDaMaquinaDeEstados() =>
        AgendaService.StatusConhecidos.Should().BeEquivalentTo(
            "INTENCAO", "AGENDADO", "CONFIRMADO", "REALIZADO", "CANCELADO", "NAO_COMPARECEU");
}
