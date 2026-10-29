namespace Kura.Application.Services;

using Kura.Application.Services.Interfaces;
using Kura.Domain.Interfaces;

/// <summary>
/// S4-DB-06 — service fino sobre <see cref="IRotinasCobrancaOracle"/>. A única coisa que ele
/// decide é <b>de quem é a clínica</b>: sai de <see cref="IClinicaContext"/> (JWT) e de lugar
/// nenhum mais. O resultado do package é devolvido como veio (JSON pronto), sem reparse e sem
/// log de conteúdo (dado de cobrança).
/// </summary>
public class CobrancaOracleService : ICobrancaOracleService
{
    private readonly IRotinasCobrancaOracle _rotinas;
    private readonly IClinicaContext _clinicaContext;

    public CobrancaOracleService(IRotinasCobrancaOracle rotinas, IClinicaContext clinicaContext)
    {
        _rotinas = rotinas;
        _clinicaContext = clinicaContext;
    }

    public Task<string> ExportarJsonAsync(CancellationToken ct = default) =>
        _rotinas.ListarJsonAsync(_clinicaContext.IdClinica, ct);

    public Task<string> RelatorioJsonAsync(CancellationToken ct = default) =>
        _rotinas.RelatorioJsonAsync(_clinicaContext.IdClinica, ct);
}
