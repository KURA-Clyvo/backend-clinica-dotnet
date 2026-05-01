namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class VeterinarioConfiguration : IEntityTypeConfiguration<Veterinario>
{
    public void Configure(EntityTypeBuilder<Veterinario> builder)
    {
        builder.ToTable("VETERINARIO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_VETERINARIO.NEXTVAL");

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.NmVeterinario)
            .HasColumnName("NM_VETERINARIO")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.NrCrmv)
            .HasColumnName("NR_CRMV")
            .HasMaxLength(20)
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

        builder.HasIndex(e => e.NrCrmv).IsUnique();

        builder.HasOne(e => e.Clinica)
            .WithMany(c => c.Veterinarios)
            .HasForeignKey(e => e.IdClinica)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
