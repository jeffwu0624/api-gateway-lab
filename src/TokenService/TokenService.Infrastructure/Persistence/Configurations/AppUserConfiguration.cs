using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TokenService.Domain.Entities;

namespace TokenService.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.AuthType).HasMaxLength(20).IsRequired();

        builder.Property(u => u.Roles)
               .HasConversion(
                   v => System.Text.Json.JsonSerializer.Serialize(v,
                            (System.Text.Json.JsonSerializerOptions?)null),
                   v => (IReadOnlyList<string>)System.Text.Json.JsonSerializer
                            .Deserialize<List<string>>(v,
                            (System.Text.Json.JsonSerializerOptions?)null)!,
                   new ValueComparer<IReadOnlyList<string>>(
                       (a, b) => a != null && b != null && a.SequenceEqual(b),
                       c => c.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
                       c => c.ToList().AsReadOnly()))
               .HasColumnName("RolesJson")
               .HasMaxLength(500)
               .IsRequired();
    }
}
