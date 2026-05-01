namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class MedicamentoConfiguration : IEntityTypeConfiguration<Medicamento>
{
    public void Configure(EntityTypeBuilder<Medicamento> builder)
    {
        builder.ToTable("MEDICAMENTO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_MEDICAMENTO.NEXTVAL");

        builder.Property(e => e.NmMedicamento)
            .HasColumnName("NM_MEDICAMENTO")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DsPrincipioAtivo)
            .HasColumnName("DS_PRINCIPIO_ATIVO")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.DsApresentacao)
            .HasColumnName("DS_APRESENTACAO")
            .HasMaxLength(500)
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

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
