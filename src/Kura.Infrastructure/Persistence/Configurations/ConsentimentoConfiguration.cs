namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ConsentimentoConfiguration : IEntityTypeConfiguration<Consentimento>
{
    public void Configure(EntityTypeBuilder<Consentimento> builder)
    {
        builder.ToTable("CONSENTIMENTO", tb => tb.ExcludeFromMigrations());

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID_CONSENTIMENTO");

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR")
            .IsRequired();

        builder.Property(e => e.DsTipo)
            .HasColumnName("DS_TIPO")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.StAceito)
            .HasColumnName("ST_ACEITO")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.NrVersaoTermo)
            .HasColumnName("DS_VERSAO_TERMO")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.DtConsentimento)
            .HasColumnName("DT_ACEITE")
            .IsRequired();

        // REC-15: coluna existe desde V1 (backend-tutor-java), lida agora pela 1a vez
        // do lado .NET.
        builder.Property(e => e.DtRevogacao)
            .HasColumnName("DT_REVOGACAO")
            .IsRequired(false);
    }
}
