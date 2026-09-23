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

        // Not filtered: someone the owner removed must not sign up again with the same address. An
        // erasure request overwrites the address, so it frees itself and is never blocked here.
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.Email).HasMaxLength(UserLimits.EmailLength);
        builder.Property(user => user.FirstName).HasMaxLength(UserLimits.NameLength);
        builder.Property(user => user.LastName).HasMaxLength(UserLimits.NameLength);
        builder.Property(user => user.EncryptedTotpSecret).HasMaxLength(500);
        builder.Ignore(user => user.FullName);

        // A bare join table. Who assigned a role and when belongs in the audit log.
        builder.HasMany(user => user.Roles)
            .WithMany()
            .UsingEntity(join =>
            {
                join.ToTable("user_roles");

                // WithMany has no navigation to name the key after, so EF falls back to this side's
                // navigation and the column comes out roles_id next to user_id.
                join.Property("RolesId").HasColumnName("role_id");
            });

        builder.Navigation(user => user.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(user => !user.IsDeleted);
    }
}
