using CustomChallenge.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomChallenge.Api.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)");

        // Last line of defense: even if application logic has a bug, the
        // database itself refuses to let stock go negative.
        builder.ToTable(t => t.HasCheckConstraint("CK_Products_Stock_NonNegative", "Stock >= 0"));
    }
}
