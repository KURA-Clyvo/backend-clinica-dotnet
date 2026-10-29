namespace Kura.Infrastructure.RotinasOracle;

using System.Data;
using Kura.Domain.Exceptions;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

/// <summary>
/// S4-DB-06 — chama <c>PKG_COBRANCA.LISTAR_JSON</c> / <c>RELATORIO_JSON</c> pela conexão do
/// <see cref="KuraDbContext"/>. Contrato do banco: <c>s4-db-03-revisao.md</c>.
///
/// <list type="bullet">
/// <item><description>Parâmetros <b>nesta ordem</b>: <c>p_id_clinica</c> (Int64, Input) e
/// <c>p_saida</c> (Clob, Output).</description></item>
/// <item><description>Conexão: abre se estava fechada e <b>devolve ao estado anterior</b>; se há
/// transação corrente do EF, o comando participa dela. As rotinas não fazem COMMIT/ROLLBACK.</description></item>
/// <item><description>O CLOB de saída é temporário (<c>DBMS_LOB.SESSION</c>): é lido com a
/// conexão aberta e recebe <c>Dispose</c>.</description></item>
/// <item><description>Qualquer falha vira exceção de mensagem fixa — <c>EntidadeNaoEncontradaException</c>,
/// <c>RotinaOracleIndisponivelException</c> ou <c>RotinaOracleFalhouException</c> (ver
/// <see cref="ErrosRotinaOracle"/>); nada do texto do Oracle sai daqui. Só o cancelamento
/// (<c>OperationCanceledException</c>) propaga como está.</description></item>
/// </list>
///
/// <para>🔴 A rotina que devolve todas as clínicas não é chamada em lugar nenhum desta classe
/// (teste-guarda no projeto de testes da Infrastructure).</para>
/// <para>⚠️ Chamada ao Oracle real: <b>NÃO VERIFICADA</b> na S4-DB-06 — a suíte é InMemory e não
/// executa PL/SQL. A prova é o G4 (S4-DB-08).</para>
/// </summary>
public class RotinasCobrancaOracle : IRotinasCobrancaOracle
{
    internal const string ProcListar = "PKG_COBRANCA.LISTAR_JSON";
    internal const string ProcRelatorio = "PKG_COBRANCA.RELATORIO_JSON";

    private readonly KuraDbContext _db;

    public RotinasCobrancaOracle(KuraDbContext db) => _db = db;

    public Task<string> ListarJsonAsync(long idClinica, CancellationToken ct = default) =>
        ExecutarAsync(ProcListar, idClinica, ct);

    public Task<string> RelatorioJsonAsync(long idClinica, CancellationToken ct = default) =>
        ExecutarAsync(ProcRelatorio, idClinica, ct);

    /// <summary>
    /// Monta o comando (tipo, nome da rotina e os 2 parâmetros, na ordem do contrato) <b>sem
    /// conexão</b>, para o teste unitário fixar a montagem sem Oracle. <c>internal</c> por isso.
    /// </summary>
    internal static OracleCommand MontarComando(string procedure, long idClinica)
    {
        var cmd = new OracleCommand
        {
            CommandType = CommandType.StoredProcedure,
            CommandText = procedure,
        };
        cmd.Parameters.Add(new OracleParameter("p_id_clinica", OracleDbType.Int64, idClinica, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter("p_saida", OracleDbType.Clob, ParameterDirection.Output));
        return cmd;
    }

    /// <remarks><c>internal virtual</c> só para o teste observar qual rotina cada método público pede.</remarks>
    internal virtual async Task<string> ExecutarAsync(string procedure, long idClinica, CancellationToken ct)
    {
        try
        {
            var conexao = (OracleConnection)_db.Database.GetDbConnection();
            var abriuAqui = conexao.State != ConnectionState.Open;
            if (abriuAqui)
                await conexao.OpenAsync(ct);

            try
            {
                using var cmd = MontarComando(procedure, idClinica);
                cmd.Connection = conexao;

                // Participa da transação corrente do EF, se houver (a rotina não commita).
                var transacao = _db.Database.CurrentTransaction;
                if (transacao is not null)
                    cmd.Transaction = (OracleTransaction)transacao.GetDbTransaction();

                var saida = cmd.Parameters["p_saida"];

                await cmd.ExecuteNonQueryAsync(ct);

                // Lê com a conexão ABERTA e libera o CLOB temporário.
                using var clob = saida.Value as OracleClob
                    ?? throw new RotinaOracleFalhouException();
                return clob.Value;
            }
            finally
            {
                if (abriuAqui)
                    await conexao.CloseAsync();
            }
        }
        catch (OracleException ex)
        {
            throw ErrosRotinaOracle.Traduzir(ex.Number, idClinica, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   and not DomainException
                                   and not RotinaOracleFalhouException
                                   and not RotinaOracleIndisponivelException)
        {
            // Nenhuma exceção crua escapa: o middleware poria ex.Message no title.
            throw new RotinaOracleFalhouException(ex);
        }
    }
}
