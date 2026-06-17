using Replay.Encoding.Archives;
using Replay.Models;

namespace Replay.Unreal;

public sealed class ReplayInfoReader
{
    private const uint FileMagic = 0x43F4EFDD;
    private const int MaxFriendlyNameSerializedBytes = 64 * 1024;
    private const int MaxEncryptionKeySizeBytes = 4096;

    private readonly FBinaryArchive _archive;

    public ReplayInfoReader(FBinaryArchive archive)
    {
        _archive = archive;
    }

    public ReplayInfoReadResult Read(
        ReplayInfo info,
        ReplayInfoSerializationMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(metadata);

        info.Reset();
        metadata.Reset();

        if (_archive.Length == 0)
        {
            throw new InvalidReplayInfoException("Replay info archive is empty.");
        }

        var magicNumber = _archive.ReadUInt32();
        if (magicNumber != FileMagic)
        {
            throw new InvalidReplayInfoException(
                $"Replay info magic mismatch: expected {FileMagic}, got {magicNumber}.");
        }
        
        var legacyFileVersion = _archive.ReadUInt32();

        ReadVersionMetadata(metadata, legacyFileVersion);
        ReadSummary(info, metadata);

        return new ReplayInfoReadResult(info, metadata);
    }

    private void ReadVersionMetadata(ReplayInfoSerializationMetadata metadata, uint legacyFileVersion)
    {
        if (legacyFileVersion != LocalFileReplayCustomVersions.CustomVersions)
        {
            throw new InvalidReplayInfoException(
                $"Unsupported replay info file version {legacyFileVersion}; expected {LocalFileReplayCustomVersions.CustomVersions}.");
        }

        var customVersions = _archive.ReadCustomVersionContainer();
        LocalFileReplayCustomVersions.Validate(customVersions);

        metadata.FileVersion = legacyFileVersion;
        metadata.FileCustomVersions = customVersions.Clone();
    }

    private void ReadSummary(ReplayInfo info, ReplayInfoSerializationMetadata metadata)
    {
        info.LengthInMs = _archive.ReadInt32();
        info.NetworkVersion = _archive.ReadUInt32();
        info.Changelist = _archive.ReadUInt32();

        var friendlyName = _archive.ReadFString(MaxFriendlyNameSerializedBytes);
        metadata.FileFriendlyName = friendlyName;
        info.FriendlyName = friendlyName.TrimEnd();

        info.IsLive = _archive.ReadUInt32AsBool();
        info.Timestamp = _archive.ReadInt64();
        info.Compressed = _archive.ReadUInt32AsBool();
        info.Encrypted = _archive.ReadUInt32AsBool();

        var keyPosition = _archive.Position;
        var keySize = _archive.ReadInt32();
        if (keySize is < 0 or > MaxEncryptionKeySizeBytes)
        {
            throw new InvalidReplayInfoException(
                $"Serialized encryption key size {keySize} is outside the valid range 0..{MaxEncryptionKeySizeBytes}.");
        }

        _archive.Seek(keyPosition);
        info.EncryptionKey = _archive.ReadByteArray(MaxEncryptionKeySizeBytes);

        if (info is { IsLive: false, Encrypted: true, EncryptionKey.Length: 0 })
        {
            throw new InvalidReplayInfoException("Completed replay is marked encrypted but has no key.");
        }
    }
}
