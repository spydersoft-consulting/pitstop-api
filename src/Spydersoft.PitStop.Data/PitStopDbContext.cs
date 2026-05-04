using Spydersoft.PitStop.Data.Configuration;
using Spydersoft.PitStop.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Spydersoft.PitStop.Data;

public class PitStopDbContext(DbContextOptions<PitStopDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<FillUp> FillUps => Set<FillUp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new VehicleConfiguration());
        modelBuilder.ApplyConfiguration(new FillUpConfiguration());
    }
}
