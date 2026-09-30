using System.Reflection;
using CustomChallenge.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomChallenge.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Laptop", Price = 1200m, Stock = 10, IsActive = true, Version = 1 },
            new Product { Id = 2, Name = "Keyboard", Price = 100m, Stock = 25, IsActive = true, Version = 1 },
            new Product { Id = 3, Name = "Mouse", Price = 50m, Stock = 0, IsActive = true, Version = 1 }
        );
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpProductConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BumpProductConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void BumpProductConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version++;
            }
        }
    }
}
