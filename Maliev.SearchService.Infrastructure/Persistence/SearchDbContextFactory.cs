using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.SearchService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating <see cref="SearchDbContext"/> during migrations.
/// </summary>
public class SearchDbContextFactory : IDesignTimeDbContextFactory<SearchDbContext>
{
    /// <inheritdoc/>
    public SearchDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SearchDbContext>()
            .UseNpgsql("Host=localhost;Database=search_app_db;Username=postgres;Password=password")
            .Options;

        return new SearchDbContext(options);
    }
}
