namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TipoEventoConfiguration : IEntityTypeConfiguration<TipoEvento>
{
    public void Configure(EntityTypeBuilder<TipoEvento> builder)
    {
        builder.ToTable("TIPO_EVENTO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_TIPO_EVENTO.NEXTVAL");

        builder.Property(e => e.CdTipo)
            .HasColumnName("CD_TIPO")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.NmTipo)
            .HasColumnName("NM_TIPO")
            .HasMaxLength(200)
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

        builder.HasIndex(e => e.CdTipo).IsUnique();

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
