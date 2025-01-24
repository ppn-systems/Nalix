using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notio.Database;

public sealed class NotioContextFactory
    : IDesignTimeDbContextFactory<NotioContext>
{
    public NotioContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=notio.db");

        return new NotioContext(optionsBuilder.Options);
    }
}