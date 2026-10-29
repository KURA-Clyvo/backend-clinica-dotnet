namespace Kura.Infrastructure.Persistence.Repositories;

using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Domain.Paginacao;
using Microsoft.EntityFrameworkCore;

public class PetRepository : Repository<Pet>, IPetRepository
{
    public PetRepository(KuraDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Pet>> GetByFiltersAsync(long? tutorId, long? especieId, char? porte)
    {
        var query = _dbSet.AsQueryable();

        if (tutorId.HasValue)
            query = query.Where(p => p.TutorPets.Any(tp => tp.IdTutor == tutorId.Value));

        if (especieId.HasValue)
            query = query.Where(p => p.IdEspecie == especieId.Value);

        if (porte.HasValue)
            query = query.Where(p => p.SgPorte == porte.Value);

        return await query.ToListAsync();
    }

    public async Task<Pet?> GetByIdWithTutoresAsync(long id)
    {
        return await _dbSet
            .Include(p => p.TutorPets)
                .ThenInclude(tp => tp.Tutor)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Pet?> GetByIdComVinculosAsync(long id, long idClinica)
    {
        return await _dbSet
            .Include(p => p.TutorPets)
            .FirstOrDefaultAsync(p => p.Id == id && p.IdClinica == idClinica);
    }

    public async Task<Pet?> GetByIdComTutoresAsync(long id, long idClinica)
    {
        return await _dbSet
            .Include(p => p.TutorPets)
                .ThenInclude(tp => tp.Tutor)
            .FirstOrDefaultAsync(p => p.Id == id && p.IdClinica == idClinica);
    }

    public async Task<PaginaDe<Pet>> ListarPaginadoAsync(ConsultaPets consulta, long idClinica)
    {
        // Query filter (StAtiva + tenant) continua valendo; IdClinica explícito é defesa em
        // profundidade. Nada de IgnoreQueryFilters.
        var filtrado = _dbSet.Where(p => p.IdClinica == idClinica);

        if (!string.IsNullOrWhiteSpace(consulta.Nome))
        {
            // ToLower() dos dois lados => LOWER(...) LIKE em Oracle e comparação sem caixa no InMemory.
            var nome = consulta.Nome.Trim().ToLower();
            filtrado = filtrado.Where(p => p.NmPet.ToLower().Contains(nome));
        }
        if (consulta.TutorId.HasValue)
            filtrado = filtrado.Where(p => p.TutorPets.Any(tp => tp.IdTutor == consulta.TutorId.Value));
        if (consulta.EspecieId.HasValue)
            filtrado = filtrado.Where(p => p.IdEspecie == consulta.EspecieId.Value);
        if (consulta.Porte.HasValue)
            filtrado = filtrado.Where(p => p.SgPorte == consulta.Porte.Value);

        var total = await filtrado.LongCountAsync();

        // Lista branca: o enum é fechado, nenhuma string do cliente vira ordenação.
        var ordenado = consulta.Ordem switch
        {
            OrdenacaoPet.NomeDesc => filtrado.OrderByDescending(p => p.NmPet).ThenBy(p => p.Id),
            OrdenacaoPet.DtNascimentoAsc => filtrado.OrderBy(p => p.DtNascimento).ThenBy(p => p.Id),
            OrdenacaoPet.DtNascimentoDesc => filtrado.OrderByDescending(p => p.DtNascimento).ThenBy(p => p.Id),
            OrdenacaoPet.IdAsc => filtrado.OrderBy(p => p.Id),
            OrdenacaoPet.IdDesc => filtrado.OrderByDescending(p => p.Id),
            _ => filtrado.OrderBy(p => p.NmPet).ThenBy(p => p.Id),
        };

        var itens = await ordenado
            .Skip((consulta.Pagina - 1) * consulta.TamanhoPagina)
            .Take(consulta.TamanhoPagina)
            .ToListAsync();
        return new PaginaDe<Pet>(itens, consulta.Pagina, consulta.TamanhoPagina, total);
    }
}
