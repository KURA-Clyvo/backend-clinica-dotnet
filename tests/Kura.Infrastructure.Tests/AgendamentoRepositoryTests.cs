namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

public class AgendamentoRepositoryTests
{
    private KuraDbContext CreateContext()
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    [Fact]
    public async Task GetRecentesAsync_RetornaApenasAgendamentosAnterioresAReferencia_OrdenadosDoMaisRecenteAoMaisAntigo_RespeitandoOLimite()
    {
        // Arrange
        var ctx = CreateContext();
        var referencia = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Passado-3dias", DtAgendamento = referencia.AddDays(-3) },
            new Agendamento { Id = 2, IdClinica = 1, NmPaciente = "Passado-1dia", DtAgendamento = referencia.AddDays(-1) },
            new Agendamento { Id = 3, IdClinica = 1, NmPaciente = "Passado-2dias", DtAgendamento = referencia.AddDays(-2) },
            new Agendamento { Id = 4, IdClinica = 1, NmPaciente = "Futuro", DtAgendamento = referencia.AddDays(1) });
        await ctx.SaveChangesAsync();

        var repository = new AgendamentoRepository(ctx);

        // Act
        var resultado = (await repository.GetRecentesAsync(1, referencia, 2)).ToList();

        // Assert
        resultado.Should().HaveCount(2);
        resultado.Select(a => a.NmPaciente).Should().ContainInOrder("Passado-1dia", "Passado-2dias");
        resultado.Should().NotContain(a => a.NmPaciente == "Futuro");
    }

    /// <summary>
    /// FD-17 item 1 -- MUTAÇÃO OBRIGATÓRIA. <c>Agendamento</c> é a única entidade fora de
    /// <c>ApplyTenantFilters</c>; sem o predicado explícito de <c>IdClinica</c> dentro do
    /// repositório, este teste falha (confirmado abaixo, antes do fix, revertendo
    /// temporariamente <c>AgendamentoRepository.cs</c>: os 2 agendamentos da clínica 2
    /// vazavam no resultado da clínica 1). Setup com DUAS clínicas de propósito -- um setup de
    /// clínica única passaria mesmo sem a correção.
    /// </summary>
    [Fact]
    public async Task GetRecentesAsync_ComAgendamentosDeDuasClinicas_RetornaApenasOsDaClinicaPedida()
    {
        // Arrange
        var ctx = CreateContext();
        var referencia = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Clinica1-A", DtAgendamento = referencia.AddDays(-1) },
            new Agendamento { Id = 2, IdClinica = 1, NmPaciente = "Clinica1-B", DtAgendamento = referencia.AddDays(-2) },
            new Agendamento { Id = 3, IdClinica = 2, NmPaciente = "Clinica2-A", DtAgendamento = referencia.AddDays(-1) },
            new Agendamento { Id = 4, IdClinica = 2, NmPaciente = "Clinica2-B", DtAgendamento = referencia.AddDays(-2) });
        await ctx.SaveChangesAsync();

        var repository = new AgendamentoRepository(ctx);

        // Act
        var resultadoClinica1 = (await repository.GetRecentesAsync(1, referencia, 10)).ToList();

        // Assert
        resultadoClinica1.Should().HaveCount(2);
        resultadoClinica1.Select(a => a.NmPaciente).Should().OnlyContain(nm => nm!.StartsWith("Clinica1"));
        resultadoClinica1.Should().NotContain(a => a.IdClinica == 2);
    }

    /// <summary>
    /// REC-08 (call site #2 do G0 item 3) -- MUTAÇÃO OBRIGATÓRIA. Antes deste ajuste, o
    /// método usava <c>DateTime.UtcNow</c> DIRETO no corte de futuro, ignorando o parâmetro
    /// recebido -- "2º UtcNow escondido no repositório". <c>agora</c> aqui é DELIBERADAMENTE
    /// uma data no PASSADO distante em relação ao UtcNow real desta máquina (2020, não 2026+):
    /// sob o código antigo, <c>DtAgendamento(2020) >= DateTime.UtcNow(real, ~2026)</c> é FALSO
    /// e a linha some; sob o código novo, <c>DtAgendamento(2020) >= agora(2020)</c> é
    /// verdadeiro e ela aparece. Só passa se o método usar de fato o parâmetro recebido.
    /// </summary>
    [Fact]
    public async Task GetProximosDoDiaAsync_UsaOParametroAgoraRecebido_NaoODateTimeUtcNowReal()
    {
        // Arrange
        var ctx = CreateContext();
        var agora = new DateTime(2020, 5, 10, 9, 0, 0);

        ctx.Agendamentos.Add(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Passado-real-mas-futuro-do-agora", DtAgendamento = agora });
        await ctx.SaveChangesAsync();

        var repository = new AgendamentoRepository(ctx);

        // Act
        var resultado = (await repository.GetProximosDoDiaAsync(1, agora, 10)).ToList();

        // Assert
        resultado.Should().ContainSingle();
        resultado[0].NmPaciente.Should().Be("Passado-real-mas-futuro-do-agora");
    }

    /// <summary>FD-17 item 1 -- mesma mutação, para <c>GetProximosDoDiaAsync</c>.</summary>
    [Fact]
    public async Task GetProximosDoDiaAsync_ComAgendamentosDeDuasClinicas_RetornaApenasOsDaClinicaPedida()
    {
        // Arrange -- data-alvo fixa e distante no futuro (não "hoje"), para o teste não
        // depender do horário UTC em que a suíte roda (uma data.Date perto da meia-noite fazia
        // DtAgendamento cair no dia seguinte e o teste ficar flaky). GetProximosDoDiaAsync só
        // exige DtAgendamento.Date == data.Date && DtAgendamento >= DateTime.UtcNow -- os dois
        // valem trivialmente para uma data em 2099.
        var ctx = CreateContext();
        var dataAlvo = new DateTime(2099, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, NmPaciente = "Clinica1-Hoje", DtAgendamento = dataAlvo },
            new Agendamento { Id = 2, IdClinica = 2, NmPaciente = "Clinica2-Hoje", DtAgendamento = dataAlvo });
        await ctx.SaveChangesAsync();

        var repository = new AgendamentoRepository(ctx);

        // Act
        var resultadoClinica2 = (await repository.GetProximosDoDiaAsync(2, dataAlvo, 10)).ToList();

        // Assert
        resultadoClinica2.Should().HaveCount(1);
        resultadoClinica2[0].NmPaciente.Should().Be("Clinica2-Hoje");
    }

    /// <summary>
    /// FD-17 item 3 -- <c>ContarTeleorientacoesHojeAsync</c> escopado por clínica, ancorado em
    /// <c>DT_INICIO_SESSAO</c> (não <c>DT_AGENDAMENTO</c>), e só conta quando
    /// <c>ST_TELECONSULTA</c> é verdadeiro. Também com duas clínicas para provar o isolamento.
    /// </summary>
    [Fact]
    public async Task ContarTeleorientacoesHojeAsync_ContaSoTeleconsultaDeHojeDaClinicaPedida()
    {
        // Arrange
        var ctx = CreateContext();
        var hoje = DateTime.UtcNow.Date;
        var ontem = hoje.AddDays(-1);

        ctx.Agendamentos.AddRange(
            new Agendamento { Id = 1, IdClinica = 1, StTeleconsulta = true, DtInicioSessao = DateTime.UtcNow, DtAgendamento = DateTime.UtcNow }, // conta
            new Agendamento { Id = 2, IdClinica = 1, StTeleconsulta = true, DtInicioSessao = ontem, DtAgendamento = DateTime.UtcNow }, // sessão de ontem -- não conta
            new Agendamento { Id = 3, IdClinica = 1, StTeleconsulta = false, DtInicioSessao = null, DtAgendamento = DateTime.UtcNow }, // não é teleconsulta -- não conta
            new Agendamento { Id = 4, IdClinica = 2, StTeleconsulta = true, DtInicioSessao = DateTime.UtcNow, DtAgendamento = DateTime.UtcNow }, // outra clínica -- não conta
            // 🔴 G2 da FD-17 -- as 2 linhas abaixo existem para SEPARAR duas cláusulas que a
            // fixture original confundia. Ela tinha uma única linha "negativa" não-tenant (a de
            // Id=3), e essa linha era ao mesmo tempo StTeleconsulta=false E DtInicioSessao=null
            // -- ou seja, as duas cláusulas disputavam a MESMA entrada. Medido: apagar
            // "a.StTeleconsulta" do predicado deixava 123/123 verde (EXIT=0), e apagar
            // "a.DtInicioSessao != null" também. Nenhuma das duas era vigiada.
            new Agendamento { Id = 5, IdClinica = 1, StTeleconsulta = true, DtInicioSessao = null, DtAgendamento = DateTime.UtcNow }, // marcada como teleconsulta, sessão nunca iniciada -- não conta (vigia a guarda de null)
            new Agendamento { Id = 6, IdClinica = 1, StTeleconsulta = false, DtInicioSessao = DateTime.UtcNow, DtAgendamento = DateTime.UtcNow }); // sessão hoje mas NÃO é teleconsulta -- não conta (vigia a cláusula StTeleconsulta)
        await ctx.SaveChangesAsync();

        var repository = new AgendamentoRepository(ctx);

        // Act
        var totalClinica1 = await repository.ContarTeleorientacoesHojeAsync(1, hoje);

        // Assert
        totalClinica1.Should().Be(1);
    }
}
