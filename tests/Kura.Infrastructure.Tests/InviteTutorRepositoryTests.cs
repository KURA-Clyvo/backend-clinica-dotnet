namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>
/// TASK-33 (E-1): regressão para o HasConversion de InviteTutor.NrToken.
/// Garante que o mapeamento Guid&lt;-&gt;string continua íntegro do lado da leitura (.NET),
/// já que GetByTokenAsync compara o Guid recebido contra a coluna após a conversão.
/// Não substitui a validação ponta a ponta contra Oracle real (ver task-33-report.md) —
/// o InMemory aplica o ValueConverter na materialização, mas não reproduz a serialização
/// binária que só aparece contra um provider relacional de verdade.
/// </summary>
public class InviteTutorRepositoryTests
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
    public async Task GetByTokenAsync_TokenExistente_EncontraOInviteAposConversaoGuidString()
    {
        // Arrange
        var ctx = CreateContext();
        var token = Guid.NewGuid();

        var tutor = new Tutor { Id = 1, IdClinica = 1, NmTutor = "Maria Silva", NrCpf = "12345678901" };
        ctx.Tutores.Add(tutor);
        ctx.Set<InviteTutor>().Add(new InviteTutor
        {
            Id = 1,
            IdTutor = 1,
            NrToken = token,
            DtExpiracao = DateTime.UtcNow.AddDays(7),
            DsCanal = "WHATSAPP",
            Tutor = tutor
        });
        await ctx.SaveChangesAsync();

        var repository = new InviteTutorRepository(ctx);

        // Act
        var resultado = await repository.GetByTokenAsync(token);

        // Assert
        resultado.Should().NotBeNull();
        resultado!.NrToken.Should().Be(token);
    }

    [Fact]
    public async Task GetByTokenAsync_TokenInexistente_RetornaNull()
    {
        // Arrange
        var ctx = CreateContext();
        var repository = new InviteTutorRepository(ctx);

        // Act
        var resultado = await repository.GetByTokenAsync(Guid.NewGuid());

        // Assert
        resultado.Should().BeNull();
    }

    /// <summary>
    /// REC-02 (KURA_BACKLOG_RECEPCAO.md): prova, contra um KuraDbContext real (InMemory, não
    /// mock), que <c>SoftDelete</c> (que <c>TutorService.ReemitirConviteAsync</c> usa para
    /// cancelar o invite antigo) faz o invite desaparecer de <c>GetByTokenAsync</c> — a MESMA
    /// forma de "não encontrado" que <c>OnboardingService.registrarPorInvite</c> usaria no passo
    /// 1 se o token não existisse (<c>InviteTutorRepository.findByNrToken</c> ...
    /// <c>orElseThrow(NotFoundException)</c>). Aqui é <c>HasQueryFilter(e => e.StAtiva)</c>
    /// (InviteTutorConfiguration.cs:64) quem produz o "desaparece" — não uma linha nova escrita
    /// nesta task.
    ///
    /// Complementa (não substitui) a ancoragem do lado Java em rec-02-report.md: aquela prova
    /// que <c>ST_ATIVO='N'</c> faz o Java jogar 409 "Convite cancelado." (via
    /// <c>OnboardingServiceTest.deveLancar409SeInviteCancelado</c>, rodado ao vivo); esta prova
    /// que o .NET, do lado de escrita, de fato grava esse estado a partir do mesmo
    /// <c>SoftDelete</c> genérico (<c>Repository.cs:45-50</c>).
    /// </summary>
    [Fact]
    public async Task SoftDelete_InviteAtivo_DesaparaceDeGetByTokenAsync()
    {
        // Arrange
        var ctx = CreateContext();
        var token = Guid.NewGuid();
        var tutor = new Tutor { Id = 1, IdClinica = 1, NmTutor = "Maria Silva", NrCpf = "12345678901" };
        ctx.Tutores.Add(tutor);
        var invite = new InviteTutor
        {
            Id = 1,
            IdTutor = 1,
            NrToken = token,
            DtExpiracao = DateTime.UtcNow.AddDays(7),
            DsCanal = "WHATSAPP",
            Tutor = tutor
        };
        ctx.Set<InviteTutor>().Add(invite);
        await ctx.SaveChangesAsync();

        var repository = new InviteTutorRepository(ctx);

        // Controle positivo: antes do SoftDelete, o token É encontrado.
        (await repository.GetByTokenAsync(token)).Should().NotBeNull();

        // Act — mesmo SoftDelete genérico que TutorService.ReemitirConviteAsync chama.
        repository.SoftDelete(invite);
        await ctx.SaveChangesAsync();

        // Assert — some da consulta padrão (query filter), a mesma forma de "não encontrado"
        // usada em toda leitura deste projeto para dado soft-deletado.
        (await repository.GetByTokenAsync(token)).Should().BeNull();

        // E o valor persistido, ignorando o filtro, é StAtiva=false — que BoolToSimNaoConverter
        // grava como ST_ATIVO='N' (ver rec-02-report.md para a ancoragem do lado Java).
        var bruto = await ctx.Set<InviteTutor>().IgnoreQueryFilters()
            .FirstAsync(i => i.NrToken == token);
        bruto.StAtiva.Should().BeFalse();
    }
}
