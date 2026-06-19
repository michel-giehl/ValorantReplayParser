namespace Replay.Encoding.Net;

public readonly record struct NetworkGuid(uint Value)
{
    public bool IsValid => Value != 0;

    public bool IsDefault => Value == 1;
}
