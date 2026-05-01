namespace Kura.Infrastructure.Persistence.Configurations;

using Kura.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class LogErroConfiguration : IEntityTypeConfiguration<LogErro>
{
    public void Configure(EntityTypeBuilder<LogErro> builder)
    {
        builder.ToTable("LOG_ERRO");

        builder.HasKey(e => e.IdLog);

        builder.Property(e => e.IdLog)
            .HasColumnName("ID_LOG")
            .HasDefaultValueSql("SEQ_LOG_ERRO.NEXTVAL");

        builder.Property(e => e.DsEndpoint)
            .HasColumnName("DS_ENDPOINT")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.DsMetodo)
            .HasColumnName("DS_METODO")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.DsMensagem)
            .HasColumnName("DS_MENSAGEM")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.DsStackTrace)
            .HasColumnName("DS_STACK_TRACE")
            .HasColumnType("CLOB")
            .IsRequired(false);

        builder.Property(e => e.NrStatusCode)
            .HasColumnName("NR_STATUS_CODE")
            .IsRequired();

        builder.Property(e => e.DtOcorrencia)
            .HasColumnName("DT_OCORRENCIA")
            .IsRequired();
    }
}
