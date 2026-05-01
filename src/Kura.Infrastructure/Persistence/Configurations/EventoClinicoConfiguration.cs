namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class EventoClinicoConfiguration : IEntityTypeConfiguration<EventoClinico>
{
    public void Configure(EntityTypeBuilder<EventoClinico> builder)
    {
        builder.ToTable("EVENTO_CLINICO");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID")
            .HasDefaultValueSql("SEQ_EVENTO_CLINICO.NEXTVAL");

        builder.Property(e => e.IdPet)
            .HasColumnName("ID_PET")
            .IsRequired();

        builder.Property(e => e.IdVeterinario)
            .HasColumnName("ID_VETERINARIO")
            .IsRequired();

        builder.Property(e => e.IdTipoEvento)
            .HasColumnName("ID_TIPO_EVENTO")
            .IsRequired();

        builder.Property(e => e.DtEvento)
            .HasColumnName("DT_EVENTO")
            .IsRequired();

        builder.Property(e => e.DsObservacao)
            .HasColumnName("DS_OBSERVACAO")
            .HasMaxLength(1000)
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

        builder.HasOne(e => e.Pet)
            .WithMany()
            .HasForeignKey(e => e.IdPet)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Veterinario)
            .WithMany()
            .HasForeignKey(e => e.IdVeterinario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TipoEvento)
            .WithMany()
            .HasForeignKey(e => e.IdTipoEvento)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.StAtiva == 'S');
    }
}
