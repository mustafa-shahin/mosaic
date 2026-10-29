using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mosaic.Domain.Identity;
using Mosaic.Persistence.Configurations.Authorization;

namespace Mosaic.Persistence.Configurations.Identity;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(role => role.Id);

        // Filtered, or an owner who deletes a role can never create one with that name again.
        builder.HasIndex(role => role.Name).IsUnique().HasFilter("is_deleted = false");

        builder.Property(role => role.Name).HasMaxLength(Role.NameMaxLength);
        builder.Property(role => role.Description).HasMaxLength(Role.DescriptionMaxLength);

        // A Postgres unique index allows many nulls, and with no filter a deleted seeded role keeps its key, so a
        // reseed that cannot see deleted rows fails here instead of adding a second row.
        builder.Property(role => role.SeedKey).HasMaxLength(Role.SeedKeyMaxLength);
        builder.HasIndex(role => role.SeedKey).IsUnique();

        builder.Property(role => role.Permissions)
            .HasConversion(PermissionListConverter.Converter, PermissionListConverter.Comparer)
            .HasColumnType("text[]")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(role => !role.IsDeleted);
    }
}
