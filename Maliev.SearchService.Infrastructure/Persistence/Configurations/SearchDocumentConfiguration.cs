using Maliev.SearchService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.SearchService.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="SearchDocument"/>.
/// </summary>
public class SearchDocumentConfiguration : IEntityTypeConfiguration<SearchDocument>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SearchDocument> builder)
    {
        builder.ToTable("search_documents");
        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id).HasColumnName("id");
        builder.Property(document => document.SourceService).HasColumnName("source_service").HasMaxLength(100).IsRequired();
        builder.Property(document => document.ResourceType).HasColumnName("resource_type").HasMaxLength(100).IsRequired();
        builder.Property(document => document.ResourceId).HasColumnName("resource_id").HasMaxLength(200).IsRequired();
        builder.Property(document => document.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(document => document.Subtitle).HasColumnName("subtitle").HasMaxLength(500);
        builder.Property(document => document.Summary).HasColumnName("summary").HasMaxLength(2000);
        builder.Property(document => document.Keywords).HasColumnName("keywords").HasMaxLength(2000);
        builder.Property(document => document.Status).HasColumnName("status").HasMaxLength(100);
        builder.Property(document => document.RequiredPermission).HasColumnName("required_permission").HasMaxLength(200).IsRequired();
        builder.Property(document => document.IsDeleted).HasColumnName("is_deleted");
        builder.Property(document => document.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(document => document.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(document => new { document.SourceService, document.ResourceType, document.ResourceId })
            .IsUnique()
            .HasDatabaseName("ux_search_documents_source_resource");
        builder.HasIndex(document => document.ResourceType).HasDatabaseName("ix_search_documents_resource_type");
        builder.HasIndex(document => document.SourceService).HasDatabaseName("ix_search_documents_source_service");
        builder.HasIndex(document => document.IsDeleted).HasDatabaseName("ix_search_documents_is_deleted");
    }
}
