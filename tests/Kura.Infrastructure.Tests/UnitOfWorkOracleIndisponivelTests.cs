namespace Kura.Infrastructure.Tests;

using System.Reflection;
using FluentAssertions;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Oracle.ManagedDataAccess.Client;

/// <summary>
/// S4-DB-06 (06b2) — o <c>SaveChanges</c> do lançamento de cobrança, com trigger ou package
/// inválido, falha com <c>DbUpdateException</c> cujo inner é <c>OracleException</c>
/// 04063/04098 (ou da lista do 503). O <see cref="UnitOfWork"/> traduz só esses para
/// <see cref="RotinaOracleIndisponivelException"/>; qualquer outro erro segue como estava.
///
/// <para>O driver não expõe construtor público de <c>OracleException</c>; o helper abaixo usa o
/// construtor não público <c>(int, string, string, string, Exception)</c> (verificado: o
/// <c>Number</c> volta o código passado). Se uma atualização do driver o mudar, estes testes
/// quebram alto, não em silêncio. ⚠️ Prova a tradução, não o Oracle: NÃO VERIFICADO contra
/// Oracle real nesta task.</para>
/// </summary>
public class UnitOfWorkOracleIndisponivelTests
{
    private sealed class FalhaNoSave(Exception falha) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
            => throw falha;
    }

    private static OracleException OracleComNumero(int numero)
    {
        var ctor = typeof(OracleException).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
            [typeof(int), typeof(string), typeof(string), typeof(string), typeof(Exception)], null)
            ?? throw new InvalidOperationException("Construtor do OracleException mudou na versão do driver.");
        return (OracleException)ctor.Invoke([numero, "ds", "proc", $"ORA-{numero:D5}: texto do oracle KURA.PKG_COBRANCA", new Exception("i")]);
    }

    private static UnitOfWork Criar(Exception falha)
    {
        var clinica = new Mock<IClinicaContext>();
        clinica.Setup(c => c.IdClinicaFiltro).Returns((long?)null);
        var opts = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase($"uow-oracle-{Guid.NewGuid():N}")
            .AddInterceptors(new FalhaNoSave(falha))
            .Options;
        return new UnitOfWork(new KuraDbContext(opts, clinica.Object));
    }

    [Theory]
    [InlineData(4063)]
    [InlineData(4098)]
    [InlineData(4067)]
    [InlineData(4068)]
    [InlineData(6508)]
    [InlineData(6550)]
    public async Task DbUpdateException_com_oracle_de_indisponibilidade_vira_503(int numero)
    {
        var uow = Criar(new DbUpdateException("falhou", OracleComNumero(numero)));

        var ex = await Assert.ThrowsAsync<RotinaOracleIndisponivelException>(() => uow.CommitAsync());

        ex.Message.Should().Be("Rotina Oracle indisponível.");
        ex.Message.Should().NotContain("ORA-");
    }

    [Fact]
    public async Task OracleException_aninhada_mais_fundo_tambem_e_reconhecida()
    {
        var uow = Criar(new DbUpdateException("falhou", new InvalidOperationException("meio", OracleComNumero(4098))));

        await Assert.ThrowsAsync<RotinaOracleIndisponivelException>(() => uow.CommitAsync());
    }

    [Theory]
    [InlineData(1)]       // unicidade
    [InlineData(2291)]    // FK
    [InlineData(20001)]
    public async Task Outro_codigo_oracle_segue_como_DbUpdateException(int numero)
    {
        var original = new DbUpdateException("falhou", OracleComNumero(numero));
        var uow = Criar(original);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => uow.CommitAsync());

        ex.Should().BeSameAs(original);
    }

    [Fact]
    public async Task DbUpdateException_sem_oracle_segue_como_estava()
    {
        var original = new DbUpdateException("falhou", new InvalidOperationException("x"));
        var uow = Criar(original);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => uow.CommitAsync());

        ex.Should().BeSameAs(original);
    }

    [Fact]
    public async Task Conflito_de_concorrencia_continua_virando_ConflitoConcorrenciaException()
    {
        var uow = Criar(new DbUpdateConcurrencyException("conflito"));

        await Assert.ThrowsAsync<ConflitoConcorrenciaException>(() => uow.CommitAsync());
    }
}
