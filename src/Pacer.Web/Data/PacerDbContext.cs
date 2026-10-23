using Microsoft.EntityFrameworkCore;
using Pacer.Web.Data.Entities;

namespace Pacer.Web.Data;

public class PacerDbContext(DbContextOptions<PacerDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CounterEntity> Counters => Set<CounterEntity>();
    public DbSet<DataProtectionKeyEntity> DataProtectionKeys => Set<DataProtectionKeyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(b =>
        {
            b.HasKey(u => u.Sub);
            b.HasMany(u => u.Counters)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserSub)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CounterEntity>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(80);
        });
    }
}
