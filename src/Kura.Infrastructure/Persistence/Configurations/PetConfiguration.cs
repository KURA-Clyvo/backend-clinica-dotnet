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

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.Id)
            .HasColumnName("ID_PET")
            .HasDefaultValueSql("SEQ_PET.NEXTVAL");

        builder.Property(e => e.IdEspecie)
            .HasColumnName("ID_ESPECIE")
            .IsRequired();

        builder.Property(e => e.IdRaca)
            .HasColumnName("ID_RACA");

        builder.Property(e => e.IdVeterinarioResp)
            .HasColumnName("ID_VETERINARIO_RESP");

        builder.Property(e => e.NmPet)
            .HasColumnName("NM_PET")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(e => e.DtNascimento)
            .HasColumnName("DT_NASCIMENTO");

        builder.Property(e => e.SgSexo)
            .HasColumnName("SG_SEXO")
            .HasColumnType("CHAR(1)");

        builder.Property(e => e.SgPorte)
            .HasColumnName("SG_PORTE")
            .HasColumnType("CHAR(1)");

        // FT-01 (backend-tutor-java, V22__pet_foto.sql) já criou as 2 colunas no Oracle —
        // Flyway é a única autoridade de DDL deste projeto (CLAUDE.md); aqui só mapeamos.
        builder.Property(e => e.DsFotoChave)
            .HasColumnName("DS_FOTO_CHAVE")
            .HasMaxLength(500);

        builder.Property(e => e.DtFotoAtualizacao)
            .HasColumnName("DT_FOTO_ATUALIZACAO");

        builder.Property(e => e.StAtiva)
            .HasColumnName("ST_ATIVO")
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

    }
}
