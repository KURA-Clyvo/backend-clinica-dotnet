namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class RacaConfiguration : IEntityTypeConfiguration<Raca>
{
    public void Configure(EntityTypeBuilder<Raca> builder)
    {
        builder.ToTable("RACA");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_RACA.NEXTVAL");

        builder.Property(e => e.IdEspecie)
            .HasColumnName("ID_ESPECIE")
            .IsRequired();

        builder.Property(e => e.NmRaca)
            .HasColumnName("NM_RACA")
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

        builder.HasOne(e => e.Especie)
            .WithMany(es => es.Racas)
            .HasForeignKey(e => e.IdEspecie)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
