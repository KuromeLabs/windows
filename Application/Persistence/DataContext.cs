using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Persistence;

public class DataContext : DbContext
{
    private readonly IConfiguration _configuration;
    public DbSet<Device> Devices => Set<Device>();
    public DataContext(DbContextOptions options, IConfiguration configuration) : base(options)
    {
        _configuration = configuration;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSqlite($"Data source={_configuration["Database:Location"]}");
        
        
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Device>().HasKey(x => x.Id);
        modelBuilder.Entity<Device>().Property(x => x.Name);
    }
}