namespace Replay.Models;

public readonly record struct FVector(double X, double Y, double Z)
{
    public int Bits { get; init; }

    public int ScaleFactor { get; init; }
}

public readonly record struct FRotator(float Pitch, float Yaw, float Roll);

public readonly record struct FQuat(float X, float Y, float Z, float W);

public readonly record struct FTransform(FQuat Rotation, FVector Translation, FVector Scale3D);
