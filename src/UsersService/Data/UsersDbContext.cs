using Microsoft.EntityFrameworkCore;

namespace TcgStore.Users.Data;

public class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserEntity>(user =>
        {
            user.ToTable("users");
            user.HasKey(u => u.Id);
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.Email).HasMaxLength(254);
            user.Property(u => u.Name).HasMaxLength(100);
            user.Property(u => u.Role).HasMaxLength(20);

            // Stored as shipping_address_* columns in the users table; all NULL means no address.
            user.OwnsOne(u => u.ShippingAddress, address =>
            {
                address.Property(a => a.RecipientName).IsRequired();
                address.Property(a => a.Street).IsRequired();
                address.Property(a => a.City).IsRequired();
                address.Property(a => a.Country).IsRequired();
            });
        });
    }
}
