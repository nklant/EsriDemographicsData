using DemographicsDb.Models;
using Microsoft.EntityFrameworkCore;

namespace DemographicsDb.Context;

public class DemographicDbContext : DbContext
{
    public DemographicDbContext(DbContextOptions<DemographicDbContext> options) : base(options)
    {
    }
    
    public DbSet<DemographicsData> DemographicsData { get; set; }
    public DbSet<DataHash> DataHash { get; set; }
}
