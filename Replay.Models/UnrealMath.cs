namespace Replay.Models;

public readonly record struct FVector(float X, float Y, float Z);

public readonly record struct FQuat(float X, float Y, float Z, float W);

public readonly record struct FTransform(FQuat Rotation, FVector Translation, FVector Scale3D);
