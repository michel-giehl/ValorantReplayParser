namespace Replay.Models.Net;

public readonly record struct NetworkGuid(uint Value)
{
    public bool IsValid => Value != 0;

    public bool IsDefault => Value == 1;

    public bool IsDynamic => IsValid && (Value & 1) == 0;
}
