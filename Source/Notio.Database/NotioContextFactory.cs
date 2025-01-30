using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace Notio.Database;

public sealed class NotioContextFactory
    : IDesignTimeDbContextFactory<NotioContext>
{
    public NotioContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<NotioContext> optionsBuilder = new();

        if (string.IsNullOrEmpty(NotioContext.AzureSqlConnection))
            throw new InvalidOperationException(
                "Connection string is not set. Please configure the 'Notio_ConnectionString' environment variable.");

        optionsBuilder.UseSqlServer(NotioContext.AzureSqlConnection);

        return new NotioContext(optionsBuilder.Options);
    }
}