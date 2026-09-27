namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AgendamentoConfiguration : IEntityTypeConfiguration<Agendamento>
{
    public void Configure(EntityTypeBuilder<Agendamento> builder)
    {
        builder.ToTable("AGENDAMENTO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_AGENDAMENTO");

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.IdPet)
            .HasColumnName("ID_PET");

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR");

        builder.Property(e => e.IdVeterinario)
            .HasColumnName("ID_VETERINARIO");

        builder.Property(e => e.NmPaciente)
            .HasColumnName("NM_PACIENTE")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(e => e.DtAgendamento)
            .HasColumnName("DT_AGENDAMENTO")
            .IsRequired();

        builder.Property(e => e.NrDuracaoMinutos)
            .HasColumnName("NR_DURACAO_MINUTOS")
            .IsRequired(false);

        builder.Property(e => e.DsServico)
            .HasColumnName("DS_SERVICO")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(e => e.DsTipoConsulta)
            .HasColumnName("DS_TIPO")
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(e => e.StStatus)
            .HasColumnName("ST_STATUS")
            .HasMaxLength(50)
            .IsRequired(false);

        // M1 (REC-09/A-1): a coluna real no Oracle é VARCHAR2(20) desde a V1 — o EF
        // declarava 100, sem nunca ter escrito nela (o .NET nunca produzia DS_ORIGEM
        // antes da REC-10). CHECK novo da V23 (backend-tutor-java d1522ee):
        // DS_ORIGEM IN ('PORTAL','RECEPCAO','TRIAGEM_LUNA').
        builder.Property(e => e.DsOrigem)
            .HasColumnName("DS_ORIGEM")
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(e => e.NrVersion)
            .HasColumnName("NR_VERSION")
            .IsConcurrencyToken();

        // Flyway V5 columns
        builder.Property(e => e.DsObservacoes)
            .HasColumnName("DS_OBSERVACOES")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired(false);

        builder.Property(e => e.DtConfirmacao)
            .HasColumnName("DT_CONFIRMACAO")
            .IsRequired(false);

        builder.Property(e => e.DtCancelamento)
            .HasColumnName("DT_CANCELAMENTO")
            .IsRequired(false);

        builder.Property(e => e.DsMotivoCancel)
            .HasColumnName("DS_MOTIVO_CANCEL")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(e => e.IdEventoGerado)
            .HasColumnName("ID_EVENTO_GERADO");

        // Flyway V10 columns — teleconsulta (Daily.co)
        builder.Property(e => e.DsSalaUrl)
            .HasColumnName("DS_SALA_URL")
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(e => e.DsProvedorVideo)
            .HasColumnName("DS_PROVEDOR_VIDEO")
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(e => e.StTeleconsulta)
            .HasColumnName("ST_TELECONSULTA")
            .IsRequired();

        builder.Property(e => e.DtInicioSessao)
            .HasColumnName("DT_INICIO_SESSAO")
            .IsRequired(false);

        builder.Property(e => e.DtFimSessao)
            .HasColumnName("DT_FIM_SESSAO")
            .IsRequired(false);

        // Flyway V23 columns (backend-tutor-java d1522ee, REC-07) — recepção.
        builder.Property(e => e.DtCheckin)
            .HasColumnName("DT_CHECKIN")
            .IsRequired(false);

        builder.Property(e => e.DtInicioAtendimento)
            .HasColumnName("DT_INICIO_ATENDIMENTO")
            .IsRequired(false);

        builder.Property(e => e.IdTriagemOrigem)
            .HasColumnName("ID_TRIAGEM_ORIGEM")
            .IsRequired(false);

        builder.Property(e => e.DtLembreteConfirmacao)
            .HasColumnName("DT_LEMBRETE_CONFIRMACAO")
            .IsRequired(false);

        builder.Property(e => e.DsRespostaConfirmacao)
            .HasColumnName("DS_RESPOSTA_CONFIRMACAO")
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(e => e.DtRespostaConfirmacao)
            .HasColumnName("DT_RESPOSTA_CONFIRMACAO")
            .IsRequired(false);

        // AGENDAMENTO table (Java domain) has no ST_ATIVA column
        builder.Ignore(e => e.StAtiva);

        builder.HasOne(e => e.Pet)
            .WithMany()
            .HasForeignKey(e => e.IdPet);

        builder.HasOne(e => e.Tutor)
            .WithMany()
            .HasForeignKey(e => e.IdTutor);

        builder.HasOne(e => e.Veterinario)
            .WithMany()
            .HasForeignKey(e => e.IdVeterinario);

        // A-7/REC-09: navegação opcional para a triagem de origem (ID_TRIAGEM_ORIGEM ->
        // TRIAGEM_LUNA.ID_TRIAGEM, FK_AGEND_TRIAGEM da V23). FK opcional ⇒ EF gera LEFT
        // JOIN no Include; o HasQueryFilter de TriagemLuna (KuraDbContext) continua
        // ativo sobre a navegação — ver comentário em Agendamento.TriagemOrigem.
        builder.HasOne(e => e.TriagemOrigem)
            .WithMany()
            .HasForeignKey(e => e.IdTriagemOrigem)
            .IsRequired(false);
    }
}
