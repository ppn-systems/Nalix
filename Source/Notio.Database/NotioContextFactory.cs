using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notio.Database;

public class NotioContextFactory : IDesignTimeDbContextFactory<NotioContext>
{
    public NotioContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotioContext>();
        optionsBuilder.UseSqlite("Data Source=notio.db");

        return new NotioContext(optionsBuilder.Options);
    }
}
