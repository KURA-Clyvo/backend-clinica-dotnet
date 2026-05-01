namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable("PET");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_PET.NEXTVAL");

        builder.Property(e => e.IdEspecie)
            .HasColumnName("ID_ESPECIE")
            .IsRequired();

        builder.Property(e => e.IdRaca)
            .HasColumnName("ID_RACA")
            .IsRequired();

        builder.Property(e => e.IdVeterinarioResp)
            .HasColumnName("ID_VETERINARIO_RESP");

        builder.Property(e => e.NmPet)
            .HasColumnName("NM_PET")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DtNascimento)
            .HasColumnName("DT_NASCIMENTO")
            .IsRequired();

        builder.Property(e => e.SgSexo)
            .HasColumnName("SG_SEXO")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.SgPorte)
            .HasColumnName("SG_PORTE")
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

        builder.HasOne(e => e.Especie)
            .WithMany()
            .HasForeignKey(e => e.IdEspecie)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Raca)
            .WithMany()
            .HasForeignKey(e => e.IdRaca)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
