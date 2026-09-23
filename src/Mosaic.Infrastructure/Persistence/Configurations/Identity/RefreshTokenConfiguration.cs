using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mosaic.Domain.Identity;

namespace Mosaic.Infrastructure.Persistence.Configurations.Identity;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.HasIndex(token => token.Token).IsUnique();
        builder.HasIndex(token => token.UserId);

        builder.Property(token => token.Token).HasMaxLength(200);
        builder.Property(token => token.ReplacedByToken).HasMaxLength(200);

        // Neither side has a navigation property, so the relationship has to be stated or UserId is
        // just a column and nothing stops a token outliving its user. Cascade so erasing a person
        // takes their sessions with them.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
