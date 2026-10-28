namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Clinicas;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class ClinicaRepository : Repository<Clinica>, IClinicaRepository
{
    public ClinicaRepository(KuraDbContext context) : base(context)
    {
    }

    /// <summary>
    /// S4-FIX-02 (E67): casa a forma canônica (mascarada) OU só dígitos (linhas legadas gravadas
    /// pelo antigo ClinicaService), qualquer que seja a forma recebida.
    /// </summary>
    public async Task<bool> ExisteComCnpjAsync(string cnpj)
    {
        var canonico = NormalizadorCnpj.TentarNormalizar(cnpj, out var c) ? c : cnpj;
        var digitos = NormalizadorCnpj.SomenteDigitos(canonico);
        return await _dbSet.IgnoreQueryFilters()
            .AnyAsync(x => x.NrCnpj == canonico || x.NrCnpj == digitos);
    }

    public async Task<bool> ExisteComEmailAcessoAsync(string email) =>
        await _dbSet.IgnoreQueryFilters().AnyAsync(c => c.DsEmailAcesso == email);
}
