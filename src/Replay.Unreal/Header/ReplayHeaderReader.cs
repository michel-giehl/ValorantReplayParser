using Replay.Encoding.Archives;
using Replay.Models.Errors;
using Replay.Models.Protocol;
using Replay.Models.Replay;
using Replay.Models.Unreal;

namespace Replay.Unreal.Header;

public sealed class ReplayHeaderReader
{
    private const int CustomVersionEntryByteCount = 20;
    private const int MaxLevelNamesAndTimes = 1024;
    private const int MaxGameSpecificDataEntries = 128;

    private readonly FBinaryArchive _archive;

    public ReplayHeaderReader(FBinaryArchive archive)
    {
        _archive = archive;
    }

    public ReplayHeaderReadResult Read()
    {
        var netMagic = _archive.ReadUInt32();
        Validate(netMagic == Constants.NetworkMagic,
            $"Network magic mismatch: expected {Constants.NetworkMagic}, got {netMagic}");

        var header = new ReplayHeader
        {
            NetworkVersion = _archive.ReadUInt32(),
        };
        Validate(header.NetworkVersion == Constants.ExpectedNetworkVersion,
            $"Unexpected network version: expected {Constants.ExpectedNetworkVersion}, got {header.NetworkVersion}");

        var customVersionCount = _archive.ReadInt32();
        ValidateCustomVersionCount(customVersionCount);
        _archive.Skip(checked(customVersionCount * CustomVersionEntryByteCount));

        header.NetworkChecksum = _archive.ReadUInt32();
        header.EngineNetworkProtocolVersion = _archive.ReadUInt32();
        Validate(header.EngineNetworkProtocolVersion == Constants.ExpectedEngineNetworkProtocolVersion,
            $"Unexpected network protocol version: expected {Constants.ExpectedEngineNetworkProtocolVersion}, got {header.EngineNetworkProtocolVersion}");

        header.GameNetworkProtocolVersion = _archive.ReadUInt32();
        header.Guid = _archive.ReadGuid();

        var replayVersion = ReadReplayVersion();

        // Valorant specific bytes. Unknown what this is for
        var i = _archive.ReadUInt32();
        _archive.Skip(i);

        var ueVersion = new UEVersion
        {
            UE4Version = _archive.ReadUInt32(),
            UE5Version = _archive.ReadUInt32(),
            PackageVersionLicense = _archive.ReadUInt32(),
        };

        header.LevelNamesAndTimes = _archive.ReadTupleArray(_archive.ReadFString, _archive.ReadUInt32, MaxLevelNamesAndTimes)
            .Select(value => (LevelName: value.First, TimeInMs: value.Second))
            .ToArray();
        header.Flags = _archive.ReadUInt32AsEnum<ReplayHeaderFlags>();
        header.GameSpecificData = _archive.ReadArray(_archive.ReadFString, MaxGameSpecificDataEntries);
        header.MinRecordHz = _archive.ReadSingle();
        header.MaxRecordHz = _archive.ReadSingle();
        header.FrameLimitInMs = _archive.ReadSingle();
        header.CheckpointLimitInMs = _archive.ReadSingle();
        header.Platform = _archive.ReadFString();
        header.BuildConfig = _archive.ReadByte();
        header.BuildTargetType = _archive.ReadByteAsEnum<BuildTargetType>();

        return new ReplayHeaderReadResult(header, replayVersion, ueVersion);
    }

    private ReplayVersion ReadReplayVersion() => new()
    {
        Major = _archive.ReadUInt16(),
        Minor = _archive.ReadUInt16(),
        Patch = _archive.ReadUInt16(),
        Changelist = _archive.ReadUInt32(),
        Branch = _archive.ReadFString(),
    };


    private static void ValidateCustomVersionCount(int customVersionCount)
    {
        Validate(customVersionCount is >= 0 and <= Constants.MaxCustomVersionCount,
            $"Unexpected custom version count: expected 0..{Constants.MaxCustomVersionCount}, got {customVersionCount}");
    }

    private static void Validate(bool predicate, string message)
    {
        if (!predicate)
        {
            throw new InvalidReplayHeaderException($"Error while parsing replay header: {message}");
        }
    }
}
