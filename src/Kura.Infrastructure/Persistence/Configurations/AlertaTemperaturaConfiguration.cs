namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class AlertaTemperaturaConfiguration : IEntityTypeConfiguration<AlertaTemperatura>
{
    public void Configure(EntityTypeBuilder<AlertaTemperatura> builder)
    {
        builder.ToTable("ALERTA_TEMPERATURA");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_ALERTA_TEMPERATURA.NEXTVAL");

        builder.Property(e => e.IdLeituraTemperatura)
            .HasColumnName("ID_LEITURA_TEMPERATURA")
            .IsRequired();

        builder.Property(e => e.DsTipoAlerta)
            .HasColumnName("DS_TIPO_ALERTA")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.VlLimite)
            .HasColumnName("VL_LIMITE")
            .HasColumnType("NUMBER(5,2)")
            .IsRequired();

        builder.Property(e => e.DsMensagem)
            .HasColumnName("DS_MENSAGEM")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.StResolvido)
            .HasColumnName("ST_RESOLVIDO")
            .HasColumnType("CHAR(1)")
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

        builder.HasOne(e => e.LeituraTemperatura)
            .WithMany()
            .HasForeignKey(e => e.IdLeituraTemperatura)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
