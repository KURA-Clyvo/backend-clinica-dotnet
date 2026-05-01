namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class LeituraTemperaturaConfiguration : IEntityTypeConfiguration<LeituraTemperatura>
{
    public void Configure(EntityTypeBuilder<LeituraTemperatura> builder)
    {
        builder.ToTable("LEITURA_TEMPERATURA");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_LEITURA_TEMPERATURA.NEXTVAL");

        builder.Property(e => e.IdDispositivoIot)
            .HasColumnName("ID_DISPOSITIVO_IOT")
            .IsRequired();

        builder.Property(e => e.VlTemperatura)
            .HasColumnName("VL_TEMPERATURA")
            .HasColumnType("NUMBER(5,2)")
            .IsRequired();

        builder.Property(e => e.VlUmidade)
            .HasColumnName("VL_UMIDADE")
            .HasColumnType("NUMBER(5,2)");

        builder.Property(e => e.DtLeitura)
            .HasColumnName("DT_LEITURA")
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

        builder.HasOne(e => e.DispositivoIot)
            .WithMany(d => d.Leituras)
            .HasForeignKey(e => e.IdDispositivoIot)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
