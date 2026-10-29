namespace Kura.Application.Services.Interfaces;

/// <summary>
/// S4-DB-06 — exportação e relatório de cobranças <b>montados pelos packages Oracle</b>. A
/// clínica é sempre a do token (<c>IClinicaContext</c>); não há parâmetro de clínica.
/// </summary>
public interface ICobrancaOracleService
{
    /// <summary>JSON (array) das cobranças ativas da clínica do token.</summary>
    Task<string> ExportarJsonAsync(CancellationToken ct = default);

    /// <summary>JSON (objeto) do relatório por forma de pagamento da clínica do token.</summary>
    Task<string> RelatorioJsonAsync(CancellationToken ct = default);
}
