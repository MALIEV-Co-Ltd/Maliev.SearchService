using Maliev.SearchService.Domain.Entities;
using Maliev.SearchService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Maliev.SearchService.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for SearchService.
/// </summary>
public class SearchDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchDbContext"/> class.
    /// </summary>
    /// <param name="options">DbContext options.</param>
    public SearchDbContext(DbContextOptions<SearchDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Indexed search documents.
    /// </summary>
    public DbSet<SearchDocument> SearchDocuments => Set<SearchDocument>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new SearchDocumentConfiguration());
        modelBuilder.Entity<SearchDocument>().HasQueryFilter(d => !d.IsDeleted);
    }
}
