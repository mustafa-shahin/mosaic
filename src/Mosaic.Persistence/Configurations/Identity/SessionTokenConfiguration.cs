using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mosaic.Domain.Identity;

namespace Mosaic.Persistence.Configurations.Identity;

internal sealed class SessionTokenConfiguration : IEntityTypeConfiguration<SessionToken>
{
    public void Configure(EntityTypeBuilder<SessionToken> builder)
    {
        builder.HasKey(token => token.Id);

        // every signed-in request finds its session by this hash, and one hash names one session
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // without this, user_id is a plain column; cascade, because a session means nothing without its person
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
