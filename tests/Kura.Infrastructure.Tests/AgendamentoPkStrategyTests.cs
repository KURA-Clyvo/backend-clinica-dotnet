namespace Kura.Infrastructure.Tests;

using FluentAssertions;
using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// REC-10/A-4 — trava de metadado para a PK de <c>AGENDAMENTO</c>. A V23
/// (<c>backend-tutor-java</c> @ <c>d1522ee</c>) converteu a coluna de <c>IDENTITY</c> para
/// <c>DEFAULT SEQ_AGENDAMENTO.NEXTVAL</c> (fechando a "estratégia dupla" que colidia com o
/// Java — G0 item 1, sonda provou <c>ORA-00001</c>). Este teste garante que o <b>modelo do
/// EF</b> continua declarando o mesmo default e continua deixando o EF OMITIR a coluna no
/// INSERT — nunca gerar/mandar a PK explicitamente.
///
/// <para>
/// 🔴 <b>Por que metadado, e não um teste de INSERT contra InMemory.</b> O InMemory
/// provider gera seu PRÓPRIO valor de chave (ignorando <c>HasDefaultValueSql</c> e
/// <c>ValueGenerated</c>), então um INSERT contra ele nunca reproduziria "o EF mandou/não
/// mandou a coluna no SQL real" — o brief da REC-10 pede exatamente esta forma de prova
/// (metadado <c>ValueGenerated</c>/<c>HasDefaultValueSql</c>), mesmo raciocínio de
/// <see cref="AgendamentoV23ColumnMetadataTests"/> e do precedente em
/// <c>CobrancaTenantIsolationTests</c> (<c>GetDefaultValueSql</c> funciona independente do
/// provider, porque lê a anotação configurada, não o comportamento em runtime).
/// </para>
/// </summary>
public class AgendamentoPkStrategyTests : IClassFixture<ModeloEfInMemoryFixture>
{
    private readonly ModeloEfInMemoryFixture _modelo;

    public AgendamentoPkStrategyTests(ModeloEfInMemoryFixture modelo) => _modelo = modelo;

    [Fact]
    public void Id_DeAgendamento_DeclaraDefaultValueSql_DaSequence()
    {
        var entityType = _modelo.Modelo.FindEntityType(typeof(Agendamento))!;
        var idProp = entityType.FindProperty(nameof(Agendamento.Id))!;

        idProp.GetDefaultValueSql().Should().Be(
            "SEQ_AGENDAMENTO.NEXTVAL",
            "a V23 (backend-tutor-java d1522ee) converteu ID_AGENDAMENTO de IDENTITY para " +
            "DEFAULT SEQ_AGENDAMENTO.NEXTVAL -- nome divergente aqui reabre a colisão que a " +
            "V23 fechou (G0 item 1, ORA-00001 provado na sonda) assim que o .NET voltar a " +
            "mandar valor explícito ou a confiar num default que não existe mais.");
    }

    [Fact]
    public void Id_DeAgendamento_EhGeradoPeloBanco_NuncaExplicitoPeloEf()
    {
        var entityType = _modelo.Modelo.FindEntityType(typeof(Agendamento))!;
        var idProp = entityType.FindProperty(nameof(Agendamento.Id))!;

        // Trava exigida pelo brief: se um dia alguém acrescentar .ValueGeneratedNever() (ou
        // equivalente) na configuração, o EF volta a mandar o valor da entidade C# (que é 0
        // para uma Agendamento recém-criada em memória) no INSERT -- exatamente a colisão
        // "estratégia dupla" que a V23/A-4 fechou. ValueGenerated.OnAdd é o que faz o EF
        // OMITIR a coluna e deixar HasDefaultValueSql (acima) assumir.
        idProp.ValueGenerated.Should().Be(
            ValueGenerated.OnAdd,
            "PK long com HasDefaultValueSql precisa de ValueGenerated.OnAdd para o EF OMITIR " +
            "a coluna no INSERT -- ValueGeneratedNever faria o EF mandar o Id da entidade C# " +
            "(0 numa Agendamento nova), reabrindo a colisão com a sequence que a V23 fechou.");
    }
}
