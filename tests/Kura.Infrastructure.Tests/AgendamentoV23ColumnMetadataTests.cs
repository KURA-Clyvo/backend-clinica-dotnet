namespace Kura.Infrastructure.Tests;

using System.Linq;
using FluentAssertions;
using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// REC-09/G2 (m-5) — trava de metadado para as 6 colunas novas da V23
/// (<c>DT_CHECKIN</c>, <c>DT_INICIO_ATENDIMENTO</c>, <c>ID_TRIAGEM_ORIGEM</c>,
/// <c>DT_LEMBRETE_CONFIRMACAO</c>, <c>DS_RESPOSTA_CONFIRMACAO</c>,
/// <c>DT_RESPOSTA_CONFIRMACAO</c>), a FK opcional para <see cref="TriagemLuna"/> e os
/// <c>HasMaxLength(20)</c> de <c>DS_ORIGEM</c> (M1 do backlog) e
/// <c>DS_RESPOSTA_CONFIRMACAO</c>.
///
/// <para>
/// 🔴 <b>Por que este teste existe — medido ao vivo pela G2, não hipotético.</b> A mutação
/// M1 da revisão (<c>g2-rec09.md</c>) trocou <c>HasColumnName("DT_CHECKIN")</c> por
/// <c>"DT_CHEKIN"</c> (typo) e a suíte inteira (969 testes) continuou <b>verde</b> — o
/// provider InMemory nunca lê a anotação <c>Relational:ColumnName</c> ao montar a query, só a
/// usa o provider Oracle real. Em produção, esse mesmo typo vira <c>ORA-00904</c> em
/// <b>todo</b> <c>GET /agenda</c> (a coluna está no SELECT do <c>Include</c>), derrubando a
/// tela inteira. A mutação M3 (desfazer o <c>HasMaxLength(20)</c> de volta para 100) também
/// ficou verde pelo mesmo motivo — InMemory não valida tamanho de coluna.
/// </para>
///
/// <para>
/// Este teste lê a metadata do <see cref="IModel"/> do EF (via <c>GetColumnName</c>/
/// <c>GetMaxLength</c>), não executa SQL contra Oracle nem valida o schema real — mesmo
/// raciocínio de <see cref="InteracaoCanalColumnTypesTests"/> (que trava <c>ColumnType</c>
/// numérico), vetor diferente aqui: nome de coluna + tamanho de string + cardinalidade de FK.
/// Flyway continua sendo a única autoridade de DDL; isto só impede o MODELO do EF de regredir
/// em silêncio (SQL Oracle real fica para o G4).
/// </para>
/// </summary>
public class AgendamentoV23ColumnMetadataTests : IClassFixture<ModeloEfInMemoryFixture>
{
    private readonly ModeloEfInMemoryFixture _modelo;

    public AgendamentoV23ColumnMetadataTests(ModeloEfInMemoryFixture modelo) => _modelo = modelo;

    [Theory]
    [InlineData(nameof(Agendamento.DtCheckin), "DT_CHECKIN")]
    [InlineData(nameof(Agendamento.DtInicioAtendimento), "DT_INICIO_ATENDIMENTO")]
    [InlineData(nameof(Agendamento.IdTriagemOrigem), "ID_TRIAGEM_ORIGEM")]
    [InlineData(nameof(Agendamento.DtLembreteConfirmacao), "DT_LEMBRETE_CONFIRMACAO")]
    [InlineData(nameof(Agendamento.DsRespostaConfirmacao), "DS_RESPOSTA_CONFIRMACAO")]
    [InlineData(nameof(Agendamento.DtRespostaConfirmacao), "DT_RESPOSTA_CONFIRMACAO")]
    public void PropriedadesV23_DeAgendamento_DeclaramColumnNameAlinhadoAoFlyway(
        string nomePropriedade, string columnNameEsperado)
    {
        var entityType = _modelo.Modelo.FindEntityType(typeof(Agendamento));
        entityType.Should().NotBeNull();

        var propriedade = entityType!.FindProperty(nomePropriedade);
        propriedade.Should().NotBeNull($"Agendamento deve declarar a propriedade {nomePropriedade}");

        propriedade!.GetColumnName().Should().Be(columnNameEsperado,
            $"{nomePropriedade} deve bater com o nome real da coluna em " +
            "V23__agendamento_recepcao.sql (backend-tutor-java, migration-oracle e migration-h2)");
    }

    [Fact]
    public void DsOrigem_DeclaraMaxLength20_AlinhadoAoOracleReal()
    {
        var entityType = _modelo.Modelo.FindEntityType(typeof(Agendamento))!;
        var propriedade = entityType.FindProperty(nameof(Agendamento.DsOrigem))!;

        propriedade.GetMaxLength().Should().Be(20,
            "DS_ORIGEM é VARCHAR2(20) desde a V1 -- M1 do backlog corrigiu de 100 para 20; " +
            "reverter isso regride em silêncio sob InMemory (mutação M3 da G2 ficou verde)");
    }

    [Fact]
    public void DsRespostaConfirmacao_DeclaraMaxLength20_AlinhadoAV23()
    {
        var entityType = _modelo.Modelo.FindEntityType(typeof(Agendamento))!;
        var propriedade = entityType.FindProperty(nameof(Agendamento.DsRespostaConfirmacao))!;

        propriedade.GetMaxLength().Should().Be(20,
            "DS_RESPOSTA_CONFIRMACAO é VARCHAR2(20) na V23 (CHK_AGEND_RESP_CONF permite " +
            "NULL|SIM|CANCELAR|REMARCAR, todos <= 20 chars)");
    }

    [Fact]
    public void IdTriagemOrigem_TemForeignKeyOpcionalParaTriagemLuna()
    {
        var entityType = _modelo.Modelo.FindEntityType(typeof(Agendamento))!;

        var fk = entityType.GetForeignKeys()
            .SingleOrDefault(f =>
                f.Properties.Count == 1 &&
                f.Properties[0].Name == nameof(Agendamento.IdTriagemOrigem));

        fk.Should().NotBeNull(
            "ID_TRIAGEM_ORIGEM deve declarar FK_AGEND_TRIAGEM -> TRIAGEM_LUNA(ID_TRIAGEM)");
        fk!.PrincipalEntityType.ClrType.Should().Be(typeof(TriagemLuna));
        fk.IsRequired.Should().BeFalse(
            "ID_TRIAGEM_ORIGEM é NULL na V23 -- FK tem que ser opcional (LEFT JOIN no Include, " +
            "aceite (b) da REC-09); FK obrigatória derrubaria a linha de Agendamento inteira " +
            "quando a triagem de origem pertencesse a outra clínica, em vez de só anular a " +
            "navegação (classe do achado de TimelineRepository documentado no CLAUDE.md)");
    }
}
