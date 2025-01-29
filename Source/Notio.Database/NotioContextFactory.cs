using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Notio.Database;

public sealed class NotioContextFactory
    : IDesignTimeDbContextFactory<NotioContext>
{
    public NotioContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(NotioContext.DataSource);

        return new NotioContext(optionsBuilder.Options);
    }
}