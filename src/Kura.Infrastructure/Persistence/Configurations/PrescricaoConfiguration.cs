namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PrescricaoConfiguration : IEntityTypeConfiguration<Prescricao>
{
    public void Configure(EntityTypeBuilder<Prescricao> builder)
    {
        builder.ToTable("PRESCRICAO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_PRESCRICAO.NEXTVAL");

        builder.Property(e => e.IdEventoClinico)
            .HasColumnName("ID_EVENTO_CLINICO")
            .IsRequired();

        builder.Property(e => e.IdMedicamento)
            .HasColumnName("ID_MEDICAMENTO")
            .IsRequired();

        builder.Property(e => e.DsPosologia)
            .HasColumnName("DS_POSOLOGIA")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.NrDuracaoDias)
            .HasColumnName("NR_DURACAO_DIAS")
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

        builder.HasOne(e => e.EventoClinico)
            .WithMany()
            .HasForeignKey(e => e.IdEventoClinico)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Medicamento)
            .WithMany()
            .HasForeignKey(e => e.IdMedicamento)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
