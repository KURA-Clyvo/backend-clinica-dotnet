namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TutorPetConfiguration : IEntityTypeConfiguration<TutorPet>
{
    public void Configure(EntityTypeBuilder<TutorPet> builder)
    {
        builder.ToTable("TUTOR_PET");

        builder.HasKey(tp => new { tp.IdTutor, tp.IdPet });

        builder.Property(e => e.IdTutor)
            .HasColumnName("ID_TUTOR")
            .IsRequired();

        builder.Property(e => e.IdPet)
            .HasColumnName("ID_PET")
            .IsRequired();

        builder.Property(e => e.DsVinculo)
            .HasColumnName("DS_VINCULO")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StPrincipal)
            .HasColumnName("ST_PRINCIPAL")
            .HasColumnType("CHAR(1)")
            .IsRequired();

        builder.Property(e => e.DtVinculo)
            .HasColumnName("DT_VINCULO")
            .IsRequired();

        builder.HasOne(e => e.Tutor)
            .WithMany(t => t.TutorPets)
            .HasForeignKey(e => e.IdTutor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Pet)
            .WithMany(p => p.TutorPets)
            .HasForeignKey(e => e.IdPet)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
