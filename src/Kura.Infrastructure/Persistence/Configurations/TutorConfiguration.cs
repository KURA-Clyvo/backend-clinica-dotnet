namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TutorConfiguration : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> builder)
    {
        builder.ToTable("TUTOR");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_TUTOR")
            .HasDefaultValueSql("SEQ_TUTOR.NEXTVAL");

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.NmTutor)
            .HasColumnName("NM_TUTOR")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.NrCpf)
            .HasColumnName("NR_CPF")
            .HasMaxLength(11)
            .IsRequired();

        builder.Property(e => e.DsEmail)
            .HasColumnName("DS_EMAIL")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(e => e.NrTelefone)
            .HasColumnName("DS_TELEFONE")
            .HasMaxLength(20)
            .IsRequired();

        // REC-01 (KURA_BACKLOG_RECEPCAO.md, A-12): coluna existente desde
        // V1__initial_schema.sql:92 (backend-tutor-java), nullable, nunca mapeada antes.
        builder.Property(e => e.DsWhatsapp)
            .HasColumnName("DS_WHATSAPP")
            .HasMaxLength(20);

        builder.Property(e => e.StAvisoPrivacidade)
            .HasColumnName("ST_AVISO_PRIVACIDADE")
            .HasColumnType("CHAR(1)")
            .HasMaxLength(1)
            .IsRequired();

        builder.Property(e => e.DtAvisoPrivacidade)
            .HasColumnName("DT_AVISO_PRIVACIDADE");

        builder.Property(e => e.DsVersaoAviso)
            .HasColumnName("DS_VERSAO_AVISO")
            .HasMaxLength(20);

        builder.Property(e => e.StAtiva)
            .HasColumnName("ST_ATIVO")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CADASTRO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        builder.HasIndex(e => e.NrCpf).IsUnique();

        // TASK-21: HasQueryFilter(StAtiva) consolidado em KuraDbContext.ApplyTenantFilters
        // (também aplica filtro de tenant ID_CLINICA). Duas chamadas HasQueryFilter() para a
        // mesma entidade não se combinam com AND no EF Core 10 — a segunda sobrescreveria esta.
    }
}
