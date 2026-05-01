namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ClinicaConfiguration : IEntityTypeConfiguration<Clinica>
{
    public void Configure(EntityTypeBuilder<Clinica> builder)
    {
        builder.ToTable("CLINICA");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_CLINICA.NEXTVAL");

        builder.Property(e => e.NmClinica)
            .HasColumnName("NM_CLINICA")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.NrCnpj)
            .HasColumnName("NR_CNPJ")
            .HasMaxLength(14)
            .IsRequired();

        builder.Property(e => e.DsEndereco)
            .HasColumnName("DS_ENDERECO")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.NrTelefone)
            .HasColumnName("NR_TELEFONE")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.DsEmail)
            .HasColumnName("DS_EMAIL")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(e => e.DsSenha)
            .HasColumnName("DS_SENHA")
            .HasMaxLength(100)
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

        builder.HasIndex(e => e.NrCnpj).IsUnique();

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
