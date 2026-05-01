namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class VacinaConfiguration : IEntityTypeConfiguration<Vacina>
{
    public void Configure(EntityTypeBuilder<Vacina> builder)
    {
        builder.ToTable("VACINA");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_VACINA.NEXTVAL");

        builder.Property(e => e.IdEventoClinico)
            .HasColumnName("ID_EVENTO_CLINICO")
            .IsRequired();

        builder.Property(e => e.NmVacina)
            .HasColumnName("NM_VACINA")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.NrLote)
            .HasColumnName("NR_LOTE")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.DsFabricante)
            .HasColumnName("DS_FABRICANTE")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DtProximaDose)
            .HasColumnName("DT_PROXIMA_DOSE");

        builder.Property(e => e.StAtiva)
            .HasColumnName("ST_ATIVA")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        builder.HasOne(e => e.EventoClinico)
            .WithMany()
            .HasForeignKey(e => e.IdEventoClinico)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
