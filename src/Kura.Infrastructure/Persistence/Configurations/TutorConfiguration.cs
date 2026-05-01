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
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_TUTOR.NEXTVAL");

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
            .HasColumnName("NR_TELEFONE")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.StAtiva)
            .HasColumnName("ST_ATIVA")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        builder.HasIndex(e => e.NrCpf).IsUnique();

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
