namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;

/// <summary>S4-FIX-02 (E67): duplicidade de CNPJ detectada entre a forma mascarada e a só-dígitos (legado).</summary>
public class ClinicaRepositoryCnpjTests
{
    private static async Task<ClinicaRepository> ComClinica(string cnpjGravado)
    {
        var cc = new Mock<IClinicaContext>();
        cc.Setup(x => x.IdClinicaFiltro).Returns((long?)null);
        var options = new DbContextOptionsBuilder<KuraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var ctx = new KuraDbContext(options, cc.Object);
        ctx.Clinicas.Add(new Clinica
        {
            Id = 1, NmClinica = "C", NrCnpj = cnpjGravado, DsEndereco = "R", NmCidade = "SP", SgUf = "SP",
            NrCep = "00000000", DsEmail = "a@a.com", DsEmailAcesso = "a@a.com", DsSenhaHash = "h", StAtiva = true
        });
        await ctx.SaveChangesAsync();
        return new ClinicaRepository(ctx);
    }

    [Theory]
    [InlineData("58001200000149")]
    [InlineData("58.001.200/0001-49")]
    public async Task ExisteComCnpj_LinhaCanonica_AchaPorQualquerForma(string consulta)
    {
        var repo = await ComClinica("58.001.200/0001-49");
        (await repo.ExisteComCnpjAsync(consulta)).Should().BeTrue();
    }

    [Theory]
    [InlineData("58001200000149")]
    [InlineData("58.001.200/0001-49")]
    public async Task ExisteComCnpj_LinhaLegadaSoDigitos_AchaPorQualquerForma(string consulta)
    {
        var repo = await ComClinica("58001200000149");
        (await repo.ExisteComCnpjAsync(consulta)).Should().BeTrue();
    }

    [Fact]
    public async Task ExisteComCnpj_OutroCnpj_DevolveFalso()
    {
        var repo = await ComClinica("58.001.200/0001-49");
        (await repo.ExisteComCnpjAsync("11.222.333/0001-81")).Should().BeFalse();
    }
}
