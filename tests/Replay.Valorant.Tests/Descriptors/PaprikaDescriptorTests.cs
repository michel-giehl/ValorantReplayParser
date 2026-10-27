using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;
using Replay.Valorant.Descriptors;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.Descriptors.Agents.Paprika;

namespace Replay.Valorant.Tests.Descriptors;

public sealed class PaprikaDescriptorTests
{
    private const string ForceProxyManagerPath =
        "/Game/Characters/Paprika/S0/Ability_X/GameObject_Paprika_X_ForceProxyManager.GameObject_Paprika_X_ForceProxyManager_C";
    private const string RedirectPullProjectilePath =
        "/Game/Characters/Paprika/S0/Ability_E/Projectile_Paprika_E_RedirectPull.Projectile_Paprika_E_RedirectPull_C";

    [Test]
    public void RecordedFocusProjectilesDecodeObjectReferences()
    {
        var descriptor = new PaprikaAgentDescriptor();
        var references = (ReplicatedObjectReference?[])Decode(
            descriptor, nameof(descriptor.FocusProjectiles), "AgJeIBVEAAA=", 64)!;

        Assert.That(references.Single()!.NetGuid?.NetGuid, Is.EqualTo(4362));
    }

    [Test]
    public void RecordedForceProxyManagerDecodesAllProxyReferences()
    {
        var descriptor = Ability(ForceProxyManagerPath);
        var proxies = (ReplicatedObjectReference?[])Decode(
            descriptor,
            nameof(descriptor.Proxies),
            "KAIgIKEqAAQgIKUqAAYgIKkqAAggIK0qAAogILEqAAwgILUqAA4gILkqABAgIL0qABIgIMEqABQgIMUqABYgIMkqABggIM0qABogINEqABwgINUqAB4gINkqACAgIN0qACIgIOEqACQgIOUqACYgIOkqACggIO0qAAA=",
            976)!;

        Assert.That(proxies.Select(proxy => proxy!.NetGuid?.NetGuid),
            Is.EqualTo(Enumerable.Range(0, 20).Select(index => (uint?)(2768 + index * 2))));
    }

    [Test]
    public void RecordedRedirectSelectedPointDecodesDoubleVector()
    {
        var descriptor = Ability(RedirectPullProjectilePath);
        var field = descriptor.Fields.Single(f => f.PropertyName == nameof(descriptor.SelectedPoint));
        using var archive = new BitArchiveReader(Convert.FromBase64String("0DF14w55mkCvVVa7xaCqwMJgEE1Qs4RA"), 192);
        var context = new FieldDecodeContext();

        var point = ((IFieldDecoder)field.Decoder!).Decode(ref context, archive).VectorValue;

        Assert.Multiple(() =>
        {
            Assert.That(point.X, Is.EqualTo(1694.2645395576146));
            Assert.That(point.Y, Is.EqualTo(-3408.386194894752));
            Assert.That(point.Z, Is.EqualTo(662.41420948787));
            Assert.That(archive.AtEnd, Is.True);
        });
    }

    [Test]
    public void TruncatedRedirectSelectedPointIsRejected()
    {
        var descriptor = Ability(RedirectPullProjectilePath);
        var field = descriptor.Fields.Single(f => f.PropertyName == nameof(descriptor.SelectedPoint));
        using var archive = new BitArchiveReader(Convert.FromBase64String("0DF14w55mkCvVVa7xaCqwMJgEE1Qs4RA"), 191);
        var context = new FieldDecodeContext();

        Assert.Throws<ArchiveReadException>(() => ((IFieldDecoder)field.Decoder!).Decode(ref context, archive));
    }

    [Test]
    public void CatalogMapsPaprikaComponentInstanceNames()
    {
        var catalog = ValorantDescriptors.CreateCatalog();

        Assert.Multiple(() =>
        {
            Assert.That(catalog.SubobjectClassPaths[PaprikaRangedUsableComponentDescriptor.InstanceName],
                Is.EqualTo(PaprikaRangedUsableComponentDescriptor.ExportPath));
            Assert.That(catalog.SubobjectClassPaths[PaprikaForceProxyManagerUserDescriptor.InstanceName],
                Is.EqualTo(PaprikaForceProxyManagerUserDescriptor.ExportPath));
        });
    }

    [Test]
    public void CatalogRegistersCharacterResetRpcForPaprika()
    {
        var cache = ValorantDescriptors.CreateCatalog().ClassNetCacheDescriptors
            .Single(c => c.Path == new PaprikaAgentDescriptor().Path + "_ClassNetCache");

        Assert.That(cache.FunctionFields.Single(f => f.Name == "ClientResetRemoteMovementPrediction").Handle,
            Is.EqualTo(6));
    }

    private static AbilityActorDescriptor Ability(string path) =>
        PaprikaDescriptors.CreateDescriptors().OfType<AbilityActorDescriptor>().Single(d => d.Path == path);

    private static object? Decode(ExportGroupDescriptor descriptor, string property, string payload, int bits)
    {
        var field = descriptor.Fields.Single(f => f.PropertyName == property);
        using var archive = new BitArchiveReader(Convert.FromBase64String(payload), bits);
        var context = new FieldDecodeContext();
        var value = ((IFieldDecoder)field.Decoder!).Decode(ref context, archive).ObjectValue;
        Assert.That(archive.AtEnd, Is.True);
        return value;
    }
}
