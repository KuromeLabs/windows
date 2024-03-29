using System.Security.Cryptography.X509Certificates;
using System.Text;
using Kurome.Core.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Kurome.Core.Persistence;

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
        modelBuilder.Entity<Device>().Property(x => x.Certificate)
            .HasConversion(
                x => x.RawData,
                x => new X509Certificate2(x));
    }
}