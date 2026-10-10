using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mosaic.Domain.Identity;

namespace Mosaic.Infrastructure.Persistence.Configurations.Identity;

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

        // unique, so the second of two rotations of one token fails here even when both read the row first
        builder.HasIndex(token => token.ReplacesId).IsUnique();

        // no cascade, so a row cannot be deleted while the row that replaced it still names it
        builder.HasOne<SessionToken>()
            .WithMany()
            .HasForeignKey(token => token.ReplacesId);
    }
}
