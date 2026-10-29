namespace Kura.Infrastructure.RotinasOracle;

using Kura.Domain.Exceptions;

/// <summary>
/// S4-DB-06 — tradução <b>pura</b> de <c>OracleException.Number</c> em exceção de domínio.
/// É função de <c>int</c> (e não de <c>OracleException</c>) porque o construtor do driver não é
/// público: o teste desta função é direto, sem montar a exceção. (Os testes do <c>UnitOfWork</c>
/// a constroem por reflexão, de propósito, para exercitar o <c>catch</c> real.) Contrato do banco:
/// <c>s4-db-03-revisao.md</c>, "Contrato para
/// a S4-DB-06".
///
/// <para>🔴 Mapeia por <b>Number</b>, nunca por <c>Message</c>: a mensagem do Oracle traz
/// <c>ORA-</c> e a pilha <c>ORA-06512: at "KURA.PKG_COBRANCA"</c>, que não podem ir para o
/// <c>title</c>/<c>detail</c> da resposta. As exceções devolvidas aqui têm mensagem fixa.</para>
/// </summary>
public static class ErrosRotinaOracle
{
    /// <summary><c>p_id_clinica</c> nulo — com <c>long</c> vindo do JWT, bug do servidor (500).</summary>
    public const int ClinicaNula = 20001;

    /// <summary>Clínica inexistente ou inativa (<c>RAISE_APPLICATION_ERROR</c> do package): 404.</summary>
    public const int ClinicaInexistente = 20002;

    /// <summary>Package ausente/inválido/descartado em uso: viram 503.</summary>
    private static readonly HashSet<int> Indisponibilidade = [4063, 4067, 4068, 6508, 6550];

    /// <summary>Acrescenta 04098 (trigger inválida) para o <c>INSERT</c> de cobrança.</summary>
    private static readonly HashSet<int> IndisponibilidadeNoInsert = [4063, 4067, 4068, 4098, 6508, 6550];

    /// <summary>Traduz o erro de uma chamada de package. Nunca devolve <c>null</c>.</summary>
    public static Exception Traduzir(int number, long idClinica, Exception? origem = null) =>
        number switch
        {
            ClinicaInexistente => new EntidadeNaoEncontradaException("Clinica", idClinica),
            _ when Indisponibilidade.Contains(number) => new RotinaOracleIndisponivelException(origem),
            // 20001 e qualquer outro código: 500 de mensagem fixa.
            _ => new RotinaOracleFalhouException(origem),
        };

    /// <summary>
    /// O erro de <c>INSERT</c> de cobrança (<c>DbUpdateException</c> com inner Oracle) é
    /// "rotina indisponível"? Só estes códigos viram 503; qualquer outro segue como estava.
    /// </summary>
    public static bool IndisponivelNoInsert(int number) => IndisponibilidadeNoInsert.Contains(number);
}
