namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Kura.Domain.Interfaces;
using Kura.Infrastructure.Persistence;
using Kura.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// TASK-79 (B0.4): <c>TutorRepository.GetByTelefoneAsync</c> não tinha ORDER BY e não
/// verificava duplicidade — TUTOR.DS_TELEFONE não tem UNIQUE (V1__initial_schema.sql:91),
/// então mais de um tutor ATIVO pode compartilhar o mesmo número, inclusive dois tutores
/// da MESMA clínica (não só entre clínicas diferentes — ver
/// <see cref="MesmaClinica_DoisTutoresMesmoTelefone_RetornaNull"/> abaixo). Este é o único
/// caller sem escopo de tenant (GET /api/v1/tutores/telefone/{numero}, consumido pela IA
/// Luna por API Key, sem JWT de clínica) — antes do fix, devolver "algum" tutor colidente
/// vazava nome/id_clinica/pets da clínica ERRADA para quem perguntou pelo telefone.
///
/// Prova de mordida: os testes de colisão cross-clínica seedam tutores ativos de clínicas
/// diferentes, MESMO telefone. Contra o código anterior (FirstOrDefaultAsync sem ORDER BY
/// e sem checagem de contagem), o teste falha — devolve um Tutor (o primeiro por ordem de
/// inserção, que é o que o provider InMemory tende a preservar) em vez de null. Contra o
/// código corrigido, passa: telefone ambíguo é tratado como "não encontrado", a MESMA forma
/// já usada para telefone inexistente (ver TutorService.BuscarContextoPorTelefoneAsync —
/// controller mapeia null para 404, e do lado da Luna isso já é caminho gracioso desde a
/// TASK-77: interação gravada com id_tutor/id_clinica nulos, sem 422, resposta de fallback
/// enviada ao tutor no WhatsApp).
///
/// <see cref="MesmaClinica_DoisTutoresMesmoTelefone_RetornaNull"/> (rodada de fix 1, após
/// revisão G2) pina o caso INTRA-clínica, deliberadamente MANTIDO como "não encontrado" —
/// decisão de produto escalada ao Felipe: mesmo dentro de uma única clínica, devolver um
/// tutor arbitrário arriscaria gravar triagem (sintomas/urgência/score) no id_tutor errado,
/// o que é pior do que nenhuma triagem. Consequência real: aquele domicílio recebe o
/// fallback genérico da Luna e a interação é gravada com clínica/tutor nulos.
///
/// ⚠️ Limite declarado (InMemory ≠ Oracle): o InMemory provider do EF Core tende a
/// preservar ordem de inserção de forma estável — não reproduz o não-determinismo real que
/// FirstOrDefaultAsync sem ORDER BY teria contra Oracle (plano/ordem física pode variar
/// entre execuções). Este teste prova a parte que IMPORTA (telefone ambíguo nunca devolve
/// um tutor, colidente ou não) sob InMemory; o ORDER BY em si — que existe para nunca
/// depender dessa preservação de ordem do provider — só teria oráculo honesto de
/// não-determinismo contra Oracle real (G4 do ciclo), não nesta suíte.
/// </summary>
public class TutorRepositoryTests
{
    private static KuraDbContext CreateContext(string dbName)
    {
        var clinicaContext = new Mock<IClinicaContext>();
        clinicaContext.Setup(x => x.IdClinicaFiltro).Returns((long?)null);

        var options = new DbContextOptionsBuilder<KuraDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new KuraDbContext(options, clinicaContext.Object);
    }

    private static Clinica NovaClinica(long id, string nome) => new()
    {
        Id = id,
        NmClinica = nome,
        NrCnpj = $"0000000000{id:D4}",
        DsEndereco = "Rua Teste, 1",
        NmCidade = "Sao Paulo",
        SgUf = "SP",
        NrCep = "00000000",
        DsEmail = $"clinica{id}@teste.com",
        DsEmailAcesso = $"clinica{id}@teste.com",
        DsSenhaHash = "hash",
        StAtiva = true
    };

    private static Tutor NovoTutor(long id, long idClinica, string nome, string cpf, string telefone, bool ativo = true) => new()
    {
        Id = id,
        IdClinica = idClinica,
        NmTutor = nome,
        NrCpf = cpf,
        DsEmail = $"{nome.ToLowerInvariant()}@teste.com",
        NrTelefone = telefone,
        StAvisoPrivacidade = "S",
        DtAvisoPrivacidade = DateTime.UtcNow,
        DsVersaoAviso = "v1.0",
        StAtiva = ativo
    };

    [Fact]
    public async Task GetByTelefoneAsync_DoisTutoresAtivosClinicasDiferentesMesmoTelefone_RetornaNull()
    {
        // Arrange
        const string telefoneColidente = "5511999990000";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.AddRange(NovaClinica(1, "Clinica A"), NovaClinica(2, "Clinica B"));
            seedCtx.Tutores.AddRange(
                NovoTutor(10, idClinica: 1, "Fulano", "11122233344", telefoneColidente),
                NovoTutor(20, idClinica: 2, "Beltrano", "22233344455", telefoneColidente));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        // Act
        var resultado = await repo.GetByTelefoneAsync(telefoneColidente);

        // Assert
        resultado.Should().BeNull(
            "telefone ambíguo entre clínicas diferentes tem que ser tratado como não " +
            "encontrado — devolver qualquer um dos dois tutores vazaria dado da clínica " +
            "errada para um caller sem JWT/escopo de tenant (a IA Luna)");
    }

    [Fact]
    public async Task MesmaClinica_DoisTutoresMesmoTelefone_RetornaNull()
    {
        // Arrange
        // Rodada de fix 1 (revisão G2, Ataque 1/Important-1): a condição implementada
        // é candidatos.Count > 1 QUALQUER, não só cross-clínica — este teste pina o
        // caso INTRA-clínica (ex.: casal com o telefone da casa), que nenhum teste
        // cobria antes desta rodada. Decisão mantida de propósito (ver header da
        // classe): devolver um dos dois arriscaria gravar triagem no tutor errado, o
        // que é pior do que "não encontrado". Mutação de prova (aplicada e revertida
        // manualmente, não deixada no diff final): trocar `candidatos.Count > 1` por
        // `candidatos.Select(t => t.IdClinica).Distinct().Count() > 1` faz ESTE teste
        // falhar (2 tutores da mesma clínica não são mais tratados como colisão) sem
        // afetar os testes cross-clínica acima — ver task-79-fixround1-report.md.
        const string telefoneDaCasa = "5511955550000";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.Add(NovaClinica(1, "Clinica A"));
            seedCtx.Tutores.AddRange(
                NovoTutor(60, idClinica: 1, "ConjugeA", "10120230340", telefoneDaCasa),
                NovoTutor(61, idClinica: 1, "ConjugeB", "10120230341", telefoneDaCasa));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        // Act
        var resultado = await repo.GetByTelefoneAsync(telefoneDaCasa);

        // Assert
        resultado.Should().BeNull(
            "colisão INTRA-clínica (dois tutores ATIVOS da MESMA clínica com o mesmo " +
            "telefone) também tem que ser tratada como não encontrado — devolver um " +
            "dos dois arriscaria gravar triagem (sintomas/urgência/score) no tutor " +
            "errado, o que é pior do que nenhuma triagem");
    }

    [Fact]
    public async Task GetByTelefoneAsync_TresTutoresAtivosMesmoTelefone_RetornaNull()
    {
        // Arrange
        // Reforça que a checagem é "mais de um", não "exatamente dois" — não é um caso
        // especial de par.
        const string telefoneColidente = "5511988880000";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.AddRange(NovaClinica(1, "Clinica A"), NovaClinica(2, "Clinica B"), NovaClinica(3, "Clinica C"));
            seedCtx.Tutores.AddRange(
                NovoTutor(30, idClinica: 1, "Ana", "33344455566", telefoneColidente),
                NovoTutor(31, idClinica: 2, "Bia", "44455566677", telefoneColidente),
                NovoTutor(32, idClinica: 3, "Caio", "55566677788", telefoneColidente));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        // Act
        var resultado = await repo.GetByTelefoneAsync(telefoneColidente);

        // Assert
        resultado.Should().BeNull();
    }

    [Fact]
    public async Task GetByTelefoneAsync_UmUnicoTutorAtivoComTelefone_RetornaEsseTutor()
    {
        // Arrange
        // Controle positivo: o fix não pode transformar o caso normal (sem colisão) em
        // "não encontrado". O id esperado (7) é plantado no seed, independente do
        // caminho de produção — não é o mesmo id que a query devolveria "por acidente".
        const string telefone = "5511977770000";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.Add(NovaClinica(1, "Clinica A"));
            seedCtx.Tutores.Add(NovoTutor(7, idClinica: 1, "Fulano", "11122233344", telefone));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        // Act
        var resultado = await repo.GetByTelefoneAsync(telefone);

        // Assert
        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(7);
        resultado.IdClinica.Should().Be(1);
    }

    [Fact]
    public async Task GetByTelefoneAsync_NenhumTutorComTelefone_RetornaNull()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.Add(NovaClinica(1, "Clinica A"));
            seedCtx.Tutores.Add(NovoTutor(1, idClinica: 1, "Fulano", "11122233344", "5511900000000"));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        // Act
        var resultado = await repo.GetByTelefoneAsync("5511900001111");

        // Assert
        resultado.Should().BeNull();
    }

    [Fact]
    public async Task GetByTelefoneAsync_TutorInativoComTelefoneColidenteComAtivo_RetornaOAtivo()
    {
        // Arrange
        // Soft delete (StAtiva=false) não deve contar como colisão: o HasQueryFilter
        // global já exclui tutores inativos da query, então só o tutor ativo é
        // considerado — nenhuma ambiguidade real aqui.
        const string telefone = "5511966660000";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.AddRange(NovaClinica(1, "Clinica A"), NovaClinica(2, "Clinica B"));
            seedCtx.Tutores.AddRange(
                NovoTutor(40, idClinica: 1, "Ativo", "66677788899", telefone, ativo: true),
                NovoTutor(41, idClinica: 2, "Inativo", "77788899900", telefone, ativo: false));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        // Act
        var resultado = await repo.GetByTelefoneAsync(telefone);

        // Assert
        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(40);
    }

    [Fact]
    public async Task GetByTelefoneAsync_SentinelaNaoInformadoComColisao_RetornaNull()
    {
        // Arrange
        // TASK-79: avaliado e concluído que este caminho NÃO é alcançável pela IA Luna
        // (ela sempre chega com o número real do remetente Twilio, nunca com o literal
        // "Não informado" que TutorService.CreateAsync/UpdateAsync usam como coalesce
        // de telefone vazio — TASK-60). Mudar o schema (NrTelefone NOT NULL, V1 imutável)
        // para eliminar o sentinela é maior do que esta task justifica (nova migration
        // V17 exigiria justificativa forte) — registrado como candidato a Bloco 0 do
        // FIX_8, não corrigido aqui. Este teste prova, como defesa em profundidade, que
        // MESMO o caso latente (dois tutores sem telefone, clínicas diferentes) já sai
        // seguro do mesmo fix — sem exigir nenhum código extra.
        const string sentinela = "Não informado";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.AddRange(NovaClinica(1, "Clinica A"), NovaClinica(2, "Clinica B"));
            seedCtx.Tutores.AddRange(
                NovoTutor(50, idClinica: 1, "SemTelefoneA", "88899900011", sentinela),
                NovoTutor(51, idClinica: 2, "SemTelefoneB", "99900011122", sentinela));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        // Act
        var resultado = await repo.GetByTelefoneAsync(sentinela);

        // Assert
        resultado.Should().BeNull();
    }

    // ── ContarAtivosPorTelefoneAsync (R3a, G2b fix wave 2) ───────────────────

    [Fact]
    public async Task ContarAtivosPorTelefoneAsync_NenhumTutor_DevolveZero()
    {
        var dbName = Guid.NewGuid().ToString();
        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        var resultado = await repo.ContarAtivosPorTelefoneAsync("5511900001111");

        resultado.Should().Be(0);
    }

    [Fact]
    public async Task ContarAtivosPorTelefoneAsync_UmTutorAtivo_DevolveUm()
    {
        const string telefone = "5511988880030";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.Add(NovaClinica(1, "Clinica A"));
            seedCtx.Tutores.Add(NovoTutor(60, idClinica: 1, "Unico", "11122233355", telefone));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        var resultado = await repo.ContarAtivosPorTelefoneAsync(telefone);

        resultado.Should().Be(1);
    }

    [Fact]
    public async Task ContarAtivosPorTelefoneAsync_DoisTutoresAtivosColidentes_DevolveDois()
    {
        const string telefoneColidente = "11988880030";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.AddRange(NovaClinica(1, "Clinica A"), NovaClinica(2, "Clinica B"));
            seedCtx.Tutores.AddRange(
                NovoTutor(61, idClinica: 1, "Colidente1", "11122233366", telefoneColidente),
                NovoTutor(62, idClinica: 2, "Colidente2", "22233344477", telefoneColidente));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        var resultado = await repo.ContarAtivosPorTelefoneAsync(telefoneColidente);

        // 2, não mais que isso: é o mesmo Take(2) de GetByTelefoneAsync — só precisamos
        // distinguir 0/1/2+, nunca o total exato de colisões.
        resultado.Should().Be(2);
    }

    [Fact]
    public async Task ContarAtivosPorTelefoneAsync_TutorInativoComTelefoneColidenteComAtivo_ContaSoOAtivo()
    {
        const string telefone = "5511988880040";
        var dbName = Guid.NewGuid().ToString();

        using (var seedCtx = CreateContext(dbName))
        {
            seedCtx.Clinicas.Add(NovaClinica(1, "Clinica A"));
            seedCtx.Tutores.AddRange(
                NovoTutor(63, idClinica: 1, "Inativo", "11122233388", telefone, ativo: false),
                NovoTutor(64, idClinica: 1, "Ativo", "22233344499", telefone));
            await seedCtx.SaveChangesAsync();
        }

        using var ctx = CreateContext(dbName);
        var repo = new TutorRepository(ctx, NullLogger<TutorRepository>.Instance);

        var resultado = await repo.ContarAtivosPorTelefoneAsync(telefone);

        resultado.Should().Be(1, "o filtro global de StAtiva exclui o inativo — só o ativo conta");
    }
}
