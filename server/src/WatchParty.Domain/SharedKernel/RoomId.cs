namespace WatchParty.Domain.SharedKernel;

public readonly record struct RoomId(Guid Value)
{
    public static RoomId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
