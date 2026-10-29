namespace Kura.Domain.Interfaces;

/// <summary>
/// S4-DB-06 — porta para os packages Oracle de cobrança (<c>PKG_COBRANCA.LISTAR_JSON</c> e
/// <c>RELATORIO_JSON</c>). Mora no Domain (como <see cref="ILaudoRepository"/>) porque a
/// Infrastructure não referencia a Application.
///
/// <para>🔴 Toda operação recebe <c>long idClinica</c> — sem sobrecarga sem escopo — e
/// <c>EXPORTAR_TODAS</c> (todas as clínicas) <b>não existe nesta interface</b>, de propósito.
/// Falhas chegam como <see cref="Exceptions.EntidadeNaoEncontradaException"/> (clínica
/// inexistente), <see cref="Exceptions.RotinaOracleIndisponivelException"/> ou
/// <see cref="Exceptions.RotinaOracleFalhouException"/>; nunca como exceção do driver.</para>
/// </summary>
public interface IRotinasCobrancaOracle
{
    /// <summary>JSON (array) das cobranças ativas da clínica, montado pelo package.</summary>
    Task<string> ListarJsonAsync(long idClinica, CancellationToken ct = default);

    /// <summary>JSON (objeto) com total por forma de pagamento e subtotal da clínica.</summary>
    Task<string> RelatorioJsonAsync(long idClinica, CancellationToken ct = default);
}
