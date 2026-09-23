using Replay.Encoding.Archives;
using Replay.Models.Net;
using Replay.Models.Replay;
using Replay.Unreal.Parsing;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents.Mage.TidalWave;

namespace Replay.Valorant.Tests.Descriptors;

public class RpcExportBindingRegressionTests
{
    private const string DamageClass = "/Script/ShooterGame.DamageableComponent";
    private const string WaveClass =
        "/Game/Characters/Mage/S0/Ability_X/GameObject_Mage_X_TidalWave.GameObject_Mage_X_TidalWave_C";

    [TestCase(false)]
    [TestCase(true)]
    public void DamageRpc_PropertyExportsDoNotChangeRpcHandleWidth(bool propertiesFirst)
    {
        var registry = BindTables(DamageClass, 4, 9, "MulticastNotifyDamage_Base",
            new ReplayReleaseVersion(13, 5), propertiesFirst);

        // cdea055b-a493-4a26-8c97-7535ce2fa63e, packet 549087, channel 258.
        // The 4-entry property table previously shadowed the 9-entry RPC table.
        var data = Convert.FromHexString(
            "702E4100080000F087000800388FC8402082000508000698CC010307436ACF87CC561A" +
            "960940400048805C73511413100858C0A4141860000100000068000100000072080420" +
            "B080292930C00002000000D000020000FCE51000000002012002014002016002518002" +
            "D33A60A00204000000C00204000000E002140000000003040000002023804680101000" +
            "000000115000000080110400120480120400930000");
        using var payload = new BitArchiveReader(data, 1287);
        var context = new FieldDecodeContext
        {
            ExportGroupPath = DamageClass,
            CurrentPacketId = 549087,
            ChannelIndex = 258,
        };

        var invocations = new FieldPayloadParser().ParseClassNetCachePayload(
            payload, registry.GetBoundCache(DamageClass)!, ref context);

        Assert.That(invocations, Has.Count.EqualTo(1));
        var invocation = invocations[0];
        Assert.That(invocation.Payload, Is.TypeOf<MulticastNotifyDamageBaseParameters>());
        var damage = (MulticastNotifyDamageBaseParameters)invocation.Payload!;
        Assert.Multiple(() =>
        {
            Assert.That(payload.AtEnd, Is.True);
            Assert.That(invocation.Handle, Is.Zero);
            Assert.That(invocation.PayloadBits, Is.EqualTo(1267));
            Assert.That(invocation.ParsedBits, Is.EqualTo(invocation.PayloadBits));
            Assert.That(invocation.DecodedFieldCount, Is.EqualTo(27));
            Assert.That(damage.DamageTaken, Is.EqualTo(1));
            Assert.That(damage.DamageDealt, Is.EqualTo(999));
            Assert.That(damage.DamageKilledTarget, Is.True);
            Assert.That(damage.DamageCauser, Is.EqualTo(49356));
            Assert.That(damage.Character, Is.EqualTo(49366));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TidalWaveRpc_PropertyExportsDoNotChangeRpcHandleWidth(bool propertiesFirst)
    {
        var registry = BindTables(WaveClass, 19, 1, "MulticastStopWave",
            new ReplayReleaseVersion(13, 6), propertiesFirst);

        // be204c0f-2838-4f12-9230-cdf7e425eba1, packet 243987, channel 223.
        // The 19-entry property table previously shadowed the 1-entry RPC table.
        using var payload = new BitArchiveReader(Convert.FromHexString("6808080400"), 35);
        var context = new FieldDecodeContext
        {
            ExportGroupPath = WaveClass,
            CurrentPacketId = 243987,
            ChannelIndex = 223,
        };

        var invocations = new FieldPayloadParser().ParseClassNetCachePayload(
            payload, registry.GetBoundCache(WaveClass)!, ref context);

        Assert.That(invocations, Has.Count.EqualTo(1));
        var invocation = invocations[0];
        Assert.That(invocation.Payload, Is.TypeOf<MulticastStopWaveParameters>());
        var wave = (MulticastStopWaveParameters)invocation.Payload!;
        Assert.Multiple(() =>
        {
            Assert.That(payload.AtEnd, Is.True);
            Assert.That(invocation.Handle, Is.Zero);
            Assert.That(invocation.PayloadBits, Is.EqualTo(26));
            Assert.That(invocation.ParsedBits, Is.EqualTo(invocation.PayloadBits));
            Assert.That(invocation.DecodedFieldCount, Is.EqualTo(1));
            Assert.That(wave.FinalEndpointReached, Is.True);
        });
    }

    private static ExportBindingRegistry BindTables(
        string classPath, int propertyCount, int functionCount, string functionName,
        ReplayReleaseVersion release, bool propertiesFirst)
    {
        var registry = new ExportBindingRegistry(ValorantDescriptors.CreateCatalog(), releaseVersion: release);
        var properties = new NetFieldExportGroup
        {
            PathName = classPath,
            PathNameIndex = 1,
            NetFieldExports = new NetFieldExport?[propertyCount],
        };
        var functions = new NetFieldExportGroup
        {
            PathName = classPath + "_ClassNetCache",
            PathNameIndex = 2,
            NetFieldExports = new NetFieldExport?[functionCount],
        };
        functions.NetFieldExports[0] = new NetFieldExport
        {
            Handle = 0,
            CompatibleChecksum = 0,
            Name = functionName,
        };

        registry.OnExportGroupAdded(propertiesFirst ? properties : functions);
        registry.OnExportGroupAdded(propertiesFirst ? functions : properties);
        registry.OnExportGroupChanged(properties);

        var bound = registry.GetBoundCache(classPath);
        Assert.Multiple(() =>
        {
            Assert.That(bound, Is.SameAs(registry.GetBoundCache(functions.PathName)));
            Assert.That(bound!.FunctionsByHandle, Has.Length.EqualTo(functionCount));
            Assert.That(registry.GetBoundCacheByIndex(2), Is.SameAs(bound));
        });
        return registry;
    }
}
