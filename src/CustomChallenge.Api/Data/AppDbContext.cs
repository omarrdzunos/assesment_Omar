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
            new Product { Id = 1, Name = "Laptop", Price = 1200m, Stock = 10, IsActive = true },
            new Product { Id = 2, Name = "Keyboard", Price = 100m, Stock = 25, IsActive = true },
            new Product { Id = 3, Name = "Mouse", Price = 50m, Stock = 0, IsActive = true }
        );
    }
}
