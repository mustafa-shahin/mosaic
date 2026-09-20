using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mosaic.Domain.Identity;
using Mosaic.Infrastructure.Authorization;

namespace Mosaic.Infrastructure.Identity;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(role => role.Id);

        // Filtered, or an owner who deletes a role can never create one with that name again.
        builder.HasIndex(role => role.Name).IsUnique().HasFilter("is_deleted = false");

        builder.Property(role => role.Name).HasMaxLength(100);
        builder.Property(role => role.Description).HasMaxLength(500);

        // Postgres treats nulls as distinct in a unique index, so every owner-made role can leave it
        // empty while two rows can never claim the same seed.
        // Not filtered: a reseeder that forgets IgnoreQueryFilters should fail here, on its first run.
        builder.Property(role => role.SeedKey).HasMaxLength(100);
        builder.HasIndex(role => role.SeedKey).IsUnique();

        builder.Property(role => role.Permissions)
            .HasConversion(PermissionListConverter.Converter, PermissionListConverter.Comparer)
            .HasColumnType("text[]")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(role => !role.IsDeleted);
    }
}
