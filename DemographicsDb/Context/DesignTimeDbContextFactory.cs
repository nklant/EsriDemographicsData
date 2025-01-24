using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DemographicsDb.Context;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DemographicDbContext>
{
    public DemographicDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../DemographicsWebApi"); // Path to the WebApi project
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<DemographicDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new DemographicDbContext(optionsBuilder.Options);
    }
}
