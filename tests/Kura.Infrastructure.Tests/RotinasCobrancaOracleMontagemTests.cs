namespace Kura.Infrastructure.Tests;

using System.Data;
using FluentAssertions;
using Kura.Infrastructure.RotinasOracle;
using Oracle.ManagedDataAccess.Client;

/// <summary>
/// S4-DB-06 (fix wave, m-1) — fixa a <b>montagem</b> do comando das 2 rotinas Oracle sem abrir
/// conexão: nome, tipo, ordem/tipo/direção/valor dos parâmetros, e qual método público pede qual
/// rotina. A execução contra o Oracle real <b>NÃO VERIFICADA</b> aqui (é o G4, S4-DB-08).
/// </summary>
public class RotinasCobrancaOracleMontagemTests
{
    private sealed class Espiao : RotinasCobrancaOracle
    {
        public string? Pedida;
        public long? IdClinica;

        public Espiao() : base(null!) { }

        internal override Task<string> ExecutarAsync(string procedure, long idClinica, CancellationToken ct)
        {
            Pedida = procedure;
            IdClinica = idClinica;
            return Task.FromResult("[]");
        }
    }

    [Fact]
    public async Task ListarJsonAsync_pede_PKG_COBRANCA_LISTAR_JSON_com_a_clinica_recebida()
    {
        var e = new Espiao();
        await e.ListarJsonAsync(42);
        e.Pedida.Should().Be("PKG_COBRANCA.LISTAR_JSON");
        e.IdClinica.Should().Be(42);
    }

    [Fact]
    public async Task RelatorioJsonAsync_pede_PKG_COBRANCA_RELATORIO_JSON_com_a_clinica_recebida()
    {
        var e = new Espiao();
        await e.RelatorioJsonAsync(7);
        e.Pedida.Should().Be("PKG_COBRANCA.RELATORIO_JSON");
        e.IdClinica.Should().Be(7);
    }

    [Theory]
    [InlineData("PKG_COBRANCA.LISTAR_JSON", 11L)]
    [InlineData("PKG_COBRANCA.RELATORIO_JSON", 987654321012L)]
    public void MontarComando_fixa_tipo_nome_e_parametros_na_ordem(string rotina, long idClinica)
    {
        using var cmd = RotinasCobrancaOracle.MontarComando(rotina, idClinica);

        cmd.CommandType.Should().Be(CommandType.StoredProcedure);
        cmd.CommandText.Should().Be(rotina);
        cmd.Parameters.Count.Should().Be(2);

        var p0 = cmd.Parameters[0];
        p0.ParameterName.Should().Be("p_id_clinica");
        p0.OracleDbType.Should().Be(OracleDbType.Int64);
        p0.Direction.Should().Be(ParameterDirection.Input);
        p0.Value.Should().Be(idClinica);

        var p1 = cmd.Parameters[1];
        p1.ParameterName.Should().Be("p_saida");
        p1.OracleDbType.Should().Be(OracleDbType.Clob);
        p1.Direction.Should().Be(ParameterDirection.Output);
    }

    [Fact]
    public void Constantes_das_rotinas_tem_o_nome_exato_do_contrato()
    {
        RotinasCobrancaOracle.ProcListar.Should().Be("PKG_COBRANCA.LISTAR_JSON");
        RotinasCobrancaOracle.ProcRelatorio.Should().Be("PKG_COBRANCA.RELATORIO_JSON");
    }
}
