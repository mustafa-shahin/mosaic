namespace Mosaic.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; }

    public DateTimeOffset CreatedAt { get; protected set; }

    public DateTimeOffset? UpdatedAt { get; private set; }
}

// Only entities that hide rows instead of removing them derive from this. RefreshToken doesn't:
// its rows are deleted for real.
public abstract class SoftDeletableEntity : Entity
{
    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedByUserId { get; private set; }

    public void SoftDelete(DateTimeOffset at, Guid? byUserId)
    {
        IsDeleted = true;
        DeletedAt = at;
        DeletedByUserId = byUserId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedByUserId = null;
    }
}
