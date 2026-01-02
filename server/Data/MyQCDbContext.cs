using Microsoft.EntityFrameworkCore;
using MyQC.Models;

namespace MyQC.Data;

public class MyQCDbContext : DbContext
{
    public MyQCDbContext(DbContextOptions<MyQCDbContext> options) : base(options)
    {
    }

    public DbSet<TRSMeasurement> TRSMeasurements => Set<TRSMeasurement>();
}
