namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TriagemLunaConfiguration : IEntityTypeConfiguration<TriagemLuna>
{
    public void Configure(EntityTypeBuilder<TriagemLuna> builder)
    {
        builder.ToTable("TRIAGEM_LUNA");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_TRIAGEM")
            .HasDefaultValueSql("SEQ_TRIAGEM_LUNA.NEXTVAL");

        builder.Property(e => e.IdClinica)
            .HasColumnName("ID_CLINICA")
            .IsRequired();

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR");

        builder.Property(e => e.IdPet)
            .HasColumnName("ID_PET");

        builder.Property(e => e.DsNivelUrgencia)
            .HasColumnName("DS_NIVEL_URGENCIA")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.DsDescricao)
            .HasColumnName("DS_DESCRICAO")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.StEncaminhadoVet)
            .HasColumnName("ST_ENCAMINHADO_VET")
            .IsRequired();

        builder.Property(e => e.DtTriagem)
            .HasColumnName("DT_TRIAGEM")
            .IsRequired();

        // TASK-67: FK nullable adicionada pela V15 (backend-tutor-java) — liga a
        // triagem à interação de canal que a originou.
        builder.Property(e => e.IdInteracao)
            .HasColumnName("ID_INTERACAO");

        builder.Property(e => e.StAtiva)
            .HasColumnName("ST_ATIVA")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.DtCriacao)
            .HasColumnName("DT_CRIACAO")
            .IsRequired();

        builder.Property(e => e.DtAtualizacao)
            .HasColumnName("DT_ATUALIZACAO");

        // LU-08 / V21 (backend-tutor-java, em paralelo): colunas novas, todas
        // NULLABLE — sem IsRequired() de propósito. O .NET não gera DDL contra
        // Oracle (MIGRATIONS_POLICY.md); quem cria estas 3 colunas é a V21.
        builder.Property(e => e.NrScore)
            .HasColumnName("NR_SCORE");

        builder.Property(e => e.DsSintomas)
            .HasColumnName("DS_SINTOMAS")
            .HasMaxLength(1000);

        builder.Property(e => e.DsRegrasVersao)
            .HasColumnName("DS_REGRAS_VERSAO")
            .HasMaxLength(10);
    }
}
