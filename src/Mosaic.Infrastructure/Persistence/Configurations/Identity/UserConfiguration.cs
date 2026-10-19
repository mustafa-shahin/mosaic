using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mosaic.Application.Identity;
using Mosaic.Domain.Identity;

namespace Mosaic.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        // Not filtered, so a removed person's address never gets a second account.
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.Email).HasMaxLength(UserLimits.EmailLength);
        builder.Property(user => user.FirstName).HasMaxLength(UserLimits.NameLength);
        builder.Property(user => user.LastName).HasMaxLength(UserLimits.NameLength);
        builder.Property(user => user.EncryptedTotpSecret).HasMaxLength(500);
        builder.Ignore(user => user.FullName);

        // A bare join table, with no record of who assigned a role or when.
        builder.HasMany(user => user.Roles)
            .WithMany()
            .UsingEntity(join =>
            {
                join.ToTable("user_roles");

                // EF names this key after the Roles navigation, so it would come out roles_id next to user_id.
                join.Property("RolesId").HasColumnName("role_id");
            });

        builder.Navigation(user => user.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(user => !user.IsDeleted);
    }
}
