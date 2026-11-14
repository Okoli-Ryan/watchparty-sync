namespace WatchParty.Domain.SharedKernel;

/// <summary>An object defined by its identity rather than its attributes.</summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>Virtual so an aggregate with a composite key can derive it from its stored parts.</summary>
    public virtual TId Id { get; protected init; } = default!;

    public bool Equals(Entity<TId>? other) =>
        other is not null && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
