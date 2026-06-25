using System.Buffers.Binary;
using Replay.Encoding.Archives;
using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Models.Errors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Protocol;
using Replay.Models.Replay;
using Replay.Unreal.Channels;
using Replay.Unreal.Packets;
using Replay.Unreal.Parsing;
using Replay.Unreal.Readers;
using Replay.Unreal.World;

namespace Replay.Unreal.Tests.Bunches;

public class BunchPayloadPipelineTests
{
    private const uint TestNetGuid = 18;
    private const uint SecondTestNetGuid = 20;
    private const uint StaticTestNetGuid = 17;
    private const uint TestArchetypeGuid = 101;
    private const uint TestLevelGuid = 201;
    private const string TestPath = "/Game/Test.Test_C";
    private const string TestPlayerControllerPath = "/Game/Characters/BaseReplayController.BaseReplayController_C";
    private const string TestControllerNamedActorPath = "/Game/FakeController.FakeController_C";

    [Test]
    public void NonPartial_ZeroPayload_FramesSuccessfully()
    {
        var context = CreateContext();
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 0);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.BunchCount, Is.EqualTo(1));
            Assert.That(stats.PayloadBunchCount, Is.EqualTo(0));
            Assert.That(stats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void NonPartial_PayloadArchive_BoundedAndConsumed()
    {
        var context = CreateContext();
        AddOpenChannel(context, 1);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 1);
            w.BeginPayload();
            w.WriteBit(true);
            w.WriteBit(true);
            w.WriteIntPacked(8);
            w.WriteBits(8, 0xAB);
        });

        reader.ReadPacket(packet, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ActorContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(8));
            Assert.That(stats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void PackageMapExports_PopulateNetGuidCache()
    {
        var context = CreateContext();
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 2, bHasPackageMapExports: true);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteInt32(1);
            WriteInternalLoadObject(w, TestNetGuid, TestPath, isExporting: true);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        Assert.Multiple(() =>
        {
            Assert.That(context.NetGuidCache.PathByNetGuid, Does.ContainKey(TestNetGuid));
            Assert.That(context.NetGuidCache.PathByNetGuid[TestNetGuid], Is.EqualTo(TestPath));
            Assert.That(context.BunchPayloadStats.PackageMapExportBunchCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.ExportedNetGuidCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void PackageMapExports_RepLayoutExport_ThrowsUnsupported()
    {
        var context = CreateContext();
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 2, bHasPackageMapExports: true);
            w.BeginPayload();
            w.WriteBit(true);
        });

        Assert.Throws<InvalidReplayInfoException>(() =>
            reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload));
    }

    [Test]
    public void MustBeMappedGUIDs_ConsumedBeforeContentBlocks()
    {
        var context = CreateContext();
        AddOpenChannel(context, 3);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 3, bHasMustBeMappedGUIDs: true);
            w.BeginPayload();
            w.WriteUInt16(2);
            w.WriteIntPacked(42);
            w.WriteIntPacked(99);
            w.WriteBit(true);
            w.WriteBit(true);
            w.WriteIntPacked(0);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.MustBeMappedGuidCount, Is.EqualTo(2));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ActorContentBlockCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void OpenActorChannel_ConsumesSerializeNewActor()
    {
        var context = CreateContext();
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 4, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w);
        });

        reader.ReadPacket(packet, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.ActorChannelOpenCount, Is.EqualTo(1));
            Assert.That(stats.ActorSerializeNewActorCount, Is.EqualTo(1));
            Assert.That(context.ChannelStates, Does.ContainKey((uint)4));
        });
        var state = context.ChannelStates[4];
        Assert.Multiple(() =>
        {
            Assert.That(state.ChannelIndex, Is.EqualTo(4));
            Assert.That(state.IsOpen, Is.True);
            Assert.That(state.ActorNetGuid.Value, Is.EqualTo(TestNetGuid));
            Assert.That(state.OpenPacketId, Is.EqualTo(1));
        });
    }

    [Test]
    public void OpenActorChannel_StoresMinimalState()
    {
        var eventSink = new CapturingReplayEventSink();
        var context = CreateContext(eventSink);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w, includeLocation: true, includeRotation: true, includeScale: true, includeVelocity: true);
        });

        reader.ReadPacket(packet, 2, context.BunchPayloadPipeline.HandleBunchPayload);

        var state = context.ChannelStates[5];
        Assert.Multiple(() =>
        {
            Assert.That(state.ActorNetGuid.Value, Is.EqualTo(TestNetGuid));
            Assert.That(state.ArchetypeNetGuid.Value, Is.EqualTo(TestArchetypeGuid));
            Assert.That(state.LevelGuid.Value, Is.EqualTo(TestLevelGuid));
            Assert.That(state.SpawnLocation, Is.Not.Null);
            Assert.That(state.SpawnLocation!.Value.X, Is.EqualTo(-1000.0f));
            Assert.That(state.SpawnLocation!.Value.Y, Is.EqualTo(-300.0f));
            Assert.That(state.SpawnLocation!.Value.Z, Is.EqualTo(300.0f));
            Assert.That(state.SpawnLocation!.Value.Bits, Is.EqualTo(15));
            Assert.That(state.SpawnLocation!.Value.ScaleFactor, Is.EqualTo(10));
            Assert.That(state.SpawnRotation, Is.Not.Null);
            Assert.That(state.SpawnRotation!.Value.Pitch, Is.EqualTo(90.0f));
            Assert.That(state.SpawnRotation!.Value.Yaw, Is.EqualTo(45.0f));
            Assert.That(state.SpawnRotation!.Value.Roll, Is.EqualTo(22.5f));
            Assert.That(state.SpawnScale, Is.Not.Null);
            Assert.That(state.SpawnScale!.Value.X, Is.EqualTo(1.5f));
            Assert.That(state.SpawnScale!.Value.Bits, Is.EqualTo(64));
            Assert.That(state.SpawnVelocity, Is.Not.Null);
            Assert.That(state.SpawnVelocity!.Value.X, Is.EqualTo(10.25f));
            Assert.That(state.SpawnVelocity!.Value.Y, Is.EqualTo(-0.5f));
            Assert.That(state.SpawnVelocity!.Value.Z, Is.EqualTo(0.125f));
            Assert.That(state.SpawnVelocity!.Value.Bits, Is.EqualTo(64));
        });

        var actor = context.WorldState.ActorsByNetGuid[TestNetGuid];
        var spawned = eventSink.Events.OfType<ActorSpawned>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(actor.LifecycleStatus, Is.EqualTo(ActorLifecycleStatus.Open));
            Assert.That(actor.ChannelIndex, Is.EqualTo(5));
            Assert.That(actor.SpawnPacketId, Is.EqualTo(2));
            Assert.That(actor.Location, Is.EqualTo(state.SpawnLocation));
            Assert.That(actor.Rotation, Is.EqualTo(state.SpawnRotation));
            Assert.That(actor.Scale, Is.EqualTo(state.SpawnScale));
            Assert.That(actor.Velocity, Is.EqualTo(state.SpawnVelocity));
            Assert.That(context.BunchPayloadStats.ActorCreatedCount, Is.EqualTo(1));
            Assert.That(eventSink.Events.OfType<ActorOpened>().Count(), Is.EqualTo(1));
            Assert.That(spawned.ActorNetGuid, Is.EqualTo(TestNetGuid));
            Assert.That(spawned.Location, Is.EqualTo(state.SpawnLocation));
        });
    }

    [Test]
    public void OpenActorChannel_StaticActor_DoesNotConsumeDynamicPayloadFields()
    {
        var context = CreateContext();
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteInternalLoadObject(w, StaticTestNetGuid);
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteIntPacked(0);
        });

        reader.ReadPacket(packet, 2, context.BunchPayloadPipeline.HandleBunchPayload);

        var state = context.ChannelStates[5];
        Assert.Multiple(() =>
        {
            Assert.That(state.ActorNetGuid.Value, Is.EqualTo(StaticTestNetGuid));
            Assert.That(state.ArchetypeNetGuid.IsValid, Is.False);
            Assert.That(context.BunchPayloadStats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void OpenActorChannel_DynamicActor_FramesResidualContentPayload()
    {
        var context = CreateContext();
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w);
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteIntPacked(8);
            w.WriteBits(8, 0xAB);
        });

        reader.ReadPacket(packet, 2, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.ActorChannelOpenCount, Is.EqualTo(1));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ActorContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(8));
            Assert.That(stats.DynamicOpenPayloadBunchCount, Is.EqualTo(0));
            Assert.That(stats.DynamicOpenPayloadBitsSkipped, Is.EqualTo(0));
            Assert.That(stats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void OpenActorChannel_PlayerController_ConsumesNetPlayerIndexBeforeContentBlocks()
    {
        var context = CreateContext();
        var catalog = new DescriptorCatalog();
        catalog.Add(new TestPlayerControllerDescriptor());
        context.ExportBindingRegistry.SetCatalog(catalog);
        context.NetGuidCache.SetNetGuidPath(TestArchetypeGuid, "Default__BaseReplayController_C", new NetworkGuid(301));
        context.NetGuidCache.SetNetGuidPath(301, "/Game/Characters/_Core/BaseReplayController");
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w);
            w.WriteByte(0x02);
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteIntPacked(8);
            w.WriteBits(8, 0xAB);
        });

        reader.ReadPacket(packet, 2, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(context.ChannelStates[5].ArchetypePath, Is.EqualTo("Default__BaseReplayController_C"));
            Assert.That(context.ChannelStates[5].ReplicationClassPath, Is.EqualTo("/Game/Characters/_Core/BaseReplayController"));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ActorContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(8));
            Assert.That(stats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void OpenActorChannel_ControllerNamedActor_DoesNotConsumeNetPlayerIndex()
    {
        var context = CreateContext();
        var catalog = new DescriptorCatalog();
        catalog.Add(new ControllerNamedActorDescriptor());
        context.ExportBindingRegistry.SetCatalog(catalog);
        context.NetGuidCache.SetNetGuidPath(TestArchetypeGuid, "Default__FakeController_C");
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w);
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteIntPacked(8);
            w.WriteBits(8, 0xAB);
        });

        reader.ReadPacket(packet, 2, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(context.ChannelStates[5].ArchetypePath, Is.EqualTo("Default__FakeController_C"));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ActorContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(8));
            Assert.That(stats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void OpenActorChannel_ReopenAfterClose_ConsumesNewActor()
    {
        var context = CreateContext();
        var reader = new RawPacketReader();

        var open = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w, actorNetGuid: TestNetGuid);
        });
        reader.ReadPacket(open, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        var close = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bClose: true);
        });
        reader.ReadPacket(close, 2, context.BunchPayloadPipeline.HandleBunchPayload);

        var reopen = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w, actorNetGuid: SecondTestNetGuid);
        });
        reader.ReadPacket(reopen, 3, context.BunchPayloadPipeline.HandleBunchPayload);

        var state = context.ChannelStates[5];
        Assert.Multiple(() =>
        {
            Assert.That(context.BunchPayloadStats.ActorChannelOpenCount, Is.EqualTo(2));
            Assert.That(context.BunchPayloadStats.ActorSerializeNewActorCount, Is.EqualTo(2));
            Assert.That(state.ActorNetGuid.Value, Is.EqualTo(SecondTestNetGuid));
            Assert.That(state.IsOpen, Is.True);
            Assert.That(state.OpenPacketId, Is.EqualTo(3));
        });
    }

    [Test]
    public void CloseActorChannel_Dormancy_PreservesActorWithoutDestroyEvent()
    {
        var eventSink = new CapturingReplayEventSink();
        var context = CreateContext(eventSink);
        var reader = new RawPacketReader();

        var open = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w);
        });
        reader.ReadPacket(open, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        var close = BuildPacket(w =>
        {
            WriteBunchHeaderBits(
                w,
                chIndex: 5,
                bControl: true,
                bClose: true,
                closeReason: ChannelCloseReason.Dormancy);
        });
        reader.ReadPacket(close, 2, context.BunchPayloadPipeline.HandleBunchPayload);

        var actor = context.WorldState.ActorsByNetGuid[TestNetGuid];
        var channel = context.WorldState.Channels[5];
        Assert.Multiple(() =>
        {
            Assert.That(channel.IsOpen, Is.False);
            Assert.That(channel.IsDormant, Is.True);
            Assert.That(channel.CloseReason, Is.EqualTo(ChannelCloseReason.Dormancy));
            Assert.That(actor.LifecycleStatus, Is.EqualTo(ActorLifecycleStatus.Dormant));
            Assert.That(actor.DestroyPacketId, Is.Null);
            Assert.That(context.BunchPayloadStats.ActorChannelCloseCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.ActorDormantCount, Is.EqualTo(1));
            Assert.That(eventSink.Events.OfType<ActorClosed>().Single().Reason, Is.EqualTo(ChannelCloseReason.Dormancy));
            Assert.That(eventSink.Events.OfType<ActorDestroyed>(), Is.Empty);
        });
    }

    [Test]
    public void CloseActorChannel_Destroyed_EndsActorAndSubobjectLifetimes()
    {
        var eventSink = new CapturingReplayEventSink();
        var context = CreateContext(eventSink);
        var reader = new RawPacketReader();

        var open = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w);
        });
        reader.ReadPacket(open, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        var createSubobject = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteBit(false);
            WriteInternalLoadObject(w, netGuid: 50);
            w.WriteBit(false);
            w.WriteBit(false);
            WriteInternalLoadObject(w, netGuid: TestArchetypeGuid);
            w.WriteBit(true);
            w.WriteIntPacked(0);
        });
        reader.ReadPacket(createSubobject, 2, context.BunchPayloadPipeline.HandleBunchPayload);

        var close = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 5, bControl: true, bClose: true);
        });
        reader.ReadPacket(close, 3, context.BunchPayloadPipeline.HandleBunchPayload);

        var actor = context.WorldState.ActorsByNetGuid[TestNetGuid];
        var subobject = context.WorldState.ObjectsByNetGuid[50];
        var destroyedSubobject = eventSink.Events.OfType<SubobjectDestroyed>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(actor.LifecycleStatus, Is.EqualTo(ActorLifecycleStatus.Destroyed));
            Assert.That(actor.DestroyPacketId, Is.EqualTo(3));
            Assert.That(actor.SubobjectNetGuids, Does.Contain((uint)50));
            Assert.That(subobject.IsActive, Is.False);
            Assert.That(subobject.DestroyPacketId, Is.EqualTo(3));
            Assert.That(context.BunchPayloadStats.ActorDestroyedCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.SubobjectCreatedCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.SubobjectDestroyedCount, Is.EqualTo(1));
            Assert.That(eventSink.Events.OfType<ActorDestroyed>().Single().ActorNetGuid, Is.EqualTo(TestNetGuid));
            Assert.That(destroyedSubobject.ObjectNetGuid, Is.EqualTo(50));
            Assert.That(destroyedSubobject.DestroyedWithActor, Is.True);
        });
    }

    [Test]
    public void ContentBlock_Actor_ReadsPayloadAndSkips()
    {
        var context = CreateContext();
        AddOpenChannel(context, 6);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 6);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteIntPacked(16);
            w.WriteBits(16, 0xABCD);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ActorContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.SubobjectContentBlockCount, Is.EqualTo(0));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(16));
            Assert.That(stats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ContentBlock_ActorRepLayout_ConsumesChecksumBitBeforeFieldHandle()
    {
        CountingFieldDecoder.Reset();
        var context = CreateContext();
        var catalog = new DescriptorCatalog();
        catalog.Add(new TestContentDescriptor());
        context.ExportBindingRegistry.SetCatalog(catalog);
        context.ExportBindingRegistry.OnExportGroupChanged(CreateNetFieldExportGroup(TestPath, (0u, "FieldA")));
        context.NetGuidCache.SetNetGuidPath(TestArchetypeGuid, TestPath);
        AddOpenChannel(context, 6);
        var reader = new RawPacketReader();
        var contentBits = BuildRepLayoutPropertyBits(handle: 0, bitCount: 32, data: BitConverter.GetBytes(42));
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 6);
            w.BeginPayload();
            w.WriteBit(true);
            w.WriteBit(true);
            w.WriteIntPacked((uint)contentBits.Count);
            w.WriteBits(contentBits);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        Assert.Multiple(() =>
        {
            Assert.That(CountingFieldDecoder.DecodeCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.RepLayoutContentBlockCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.ContentPayloadBitsParsed, Is.EqualTo(contentBits.Count));
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ContentBlock_ActorRepLayout_UsesArchetypeOuterPathForDescriptorBinding()
    {
        CountingFieldDecoder.Reset();
        var context = CreateContext();
        var catalog = new DescriptorCatalog();
        catalog.Add(new TestContentDescriptor());
        context.ExportBindingRegistry.SetCatalog(catalog);
        context.ExportBindingRegistry.OnExportGroupChanged(CreateNetFieldExportGroup(TestPath, (0u, "FieldA")));
        context.NetGuidCache.SetNetGuidPath(TestArchetypeGuid, "Default__Test_C", new NetworkGuid(301));
        context.NetGuidCache.SetNetGuidPath(301, TestPath);
        var reader = new RawPacketReader();
        var contentBits = BuildRepLayoutPropertyBits(handle: 0, bitCount: 32, data: BitConverter.GetBytes(42));
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 6, bControl: true, bOpen: true);
            w.BeginPayload();
            WriteSerializeNewActor(w);
            w.WriteBit(true);
            w.WriteBit(true);
            w.WriteIntPacked((uint)contentBits.Count);
            w.WriteBits(contentBits);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var channel = context.ChannelStates[6];
        Assert.Multiple(() =>
        {
            Assert.That(channel.ArchetypePath, Is.EqualTo("Default__Test_C"));
            Assert.That(channel.ReplicationClassPath, Is.EqualTo(TestPath));
            Assert.That(CountingFieldDecoder.DecodeCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.ContentPayloadBitsSkipped, Is.EqualTo(0));
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ContentBlock_Subobject_ReadsObjectAndClass()
    {
        var eventSink = new CapturingReplayEventSink();
        var context = CreateContext(eventSink);
        AddOpenChannel(context, 7);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 7);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteBit(false);
            WriteInternalLoadObject(w, netGuid: 50);
            w.WriteBit(true);
            w.WriteIntPacked(8);
            w.WriteBits(8, 0xFF);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.SubobjectContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(8));
        });

        var objectState = context.WorldState.ObjectsByNetGuid[50];
        Assert.Multiple(() =>
        {
            Assert.That(objectState.ActorNetGuid.Value, Is.EqualTo(TestNetGuid));
            Assert.That(objectState.ChannelIndex, Is.EqualTo(7));
            Assert.That(objectState.IsStablyNamed, Is.True);
            Assert.That(objectState.IsActive, Is.True);
            Assert.That(objectState.CreatedPacketId, Is.EqualTo(0));
            Assert.That(context.WorldState.Channels[7].SubobjectNetGuids, Does.Contain((uint)50));
            Assert.That(context.BunchPayloadStats.SubobjectCreatedCount, Is.EqualTo(1));
            Assert.That(eventSink.Events.OfType<SubobjectCreated>().Single().ObjectNetGuid, Is.EqualTo(50));
        });
    }

    [Test]
    public void ContentBlock_Destroy_ConsumesFlags()
    {
        var eventSink = new CapturingReplayEventSink();
        var context = CreateContext(eventSink);
        AddOpenChannel(context, 8);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 8);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteBit(false);
            WriteInternalLoadObject(w, netGuid: 60);
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteByte(0x03);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.DeletedContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(0));
        });

        var tombstone = context.WorldState.ObjectsByNetGuid[60];
        Assert.Multiple(() =>
        {
            Assert.That(tombstone.IsActive, Is.False);
            Assert.That(tombstone.DestroyPacketId, Is.EqualTo(0));
            Assert.That(tombstone.DeleteFlags, Is.EqualTo(0x03));
            Assert.That(stats.SubobjectDestroyedCount, Is.EqualTo(1));
            Assert.That(eventSink.Events.OfType<SubobjectDestroyed>().Single().DeleteFlags, Is.EqualTo(0x03));
        });
    }

    [Test]
    public void ContentBlock_Multiple_StayAligned()
    {
        var context = CreateContext();
        AddOpenChannel(context, 9);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 9);
            w.BeginPayload();
            w.WriteBit(false); w.WriteBit(true);
            w.WriteIntPacked(4); w.WriteBits(4, 0xA);
            w.WriteBit(false); w.WriteBit(true);
            w.WriteIntPacked(8); w.WriteBits(8, 0xBC);
            w.WriteBit(false); w.WriteBit(false);
            WriteInternalLoadObject(w, netGuid: 70);
            w.WriteBit(false); w.WriteBit(true);
            w.WriteByte(0x01);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.ContentBlockCount, Is.EqualTo(3));
            Assert.That(stats.ActorContentBlockCount, Is.EqualTo(2));
            Assert.That(stats.DeletedContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(12));
            Assert.That(stats.MalformedPayloadCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void ContentBlock_PayloadOverrun_Malformed()
    {
        var context = CreateContext();
        AddOpenChannel(context, 10);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 10);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteIntPacked(1000);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.That(stats.MalformedPayloadCount, Is.EqualTo(1));
    }

    [Test]
    public void ContentBlock_SubobjectPayloadOverrun_DoesNotCreateObjectState()
    {
        var eventSink = new CapturingReplayEventSink();
        var context = CreateContext(eventSink);
        AddOpenChannel(context, 10);
        var reader = new RawPacketReader();
        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 10);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteBit(false);
            WriteInternalLoadObject(w, netGuid: 50);
            w.WriteBit(true);
            w.WriteIntPacked(1000);
        });

        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        Assert.Multiple(() =>
        {
            Assert.That(context.BunchPayloadStats.MalformedPayloadCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.MalformedContentBlockCount, Is.EqualTo(1));
            Assert.That(context.WorldState.ObjectsByNetGuid, Is.Empty);
            Assert.That(eventSink.Events, Is.Empty);
        });
    }

    [Test]
    public void Partial_InitialPlusFinal_StitchesPayload()
    {
        var context = CreateContext();
        AddOpenChannel(context, 11);
        var reader = new RawPacketReader();

        var p1 = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 11, bReliable: true, bPartial: true, bPartialInitial: true, bPartialFinal: false);
            w.BeginPayload();
            w.WriteBit(true);
            w.WriteBit(true);
            w.WriteIntPacked(16);
            w.WriteBits(6, 0x15);
        });
        reader.ReadPacket(p1, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var p2 = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 11, bReliable: true, bPartial: true, bPartialInitial: false, bPartialFinal: true);
            w.BeginPayload();
            w.WriteBits(10, 0x2A);
        });
        reader.ReadPacket(p2, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.PartialFragmentCount, Is.EqualTo(2));
            Assert.That(stats.CompletedPartialBunchCount, Is.EqualTo(1));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ActorContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(16));
            Assert.That(stats.PartialErrorCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Partial_FinalWithZeroPayload_CompletesStitchedPayload()
    {
        var context = CreateContext();
        AddOpenChannel(context, 11);
        var reader = new RawPacketReader();

        var p1 = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 11, bReliable: true, bPartial: true, bPartialInitial: true, bPartialFinal: false);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteIntPacked(6);
            w.WriteBits(6, 0x15);
        });
        reader.ReadPacket(p1, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var p2 = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 11, bReliable: true, bPartial: true, bPartialInitial: false, bPartialFinal: true);
        });
        reader.ReadPacket(p2, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.CompletedPartialBunchCount, Is.EqualTo(1));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(1));
            Assert.That(stats.ContentPayloadBitsSkipped, Is.EqualTo(6));
            Assert.That(stats.PartialErrorCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Partial_NonFinalNonBytePayload_DiscardsAccumulator()
    {
        var context = CreateContext();
        AddOpenChannel(context, 12);
        var reader = new RawPacketReader();

        var p1 = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 12, bReliable: true, bPartial: true, bPartialInitial: true, bPartialFinal: false);
            w.BeginPayload();
            w.WriteBits(6, 0x15);
        });
        reader.ReadPacket(p1, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var p2 = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 12, bReliable: true, bPartial: true, bPartialInitial: false, bPartialFinal: true);
        });
        reader.ReadPacket(p2, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.Multiple(() =>
        {
            Assert.That(stats.PartialErrorCount, Is.EqualTo(2));
            Assert.That(stats.CompletedPartialBunchCount, Is.EqualTo(0));
            Assert.That(stats.ContentBlockCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void Partial_ContinuationWithoutInitial_Error()
    {
        var context = CreateContext();
        var reader = new RawPacketReader();

        var packet = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 12, bReliable: true, bPartial: true, bPartialInitial: false, bPartialFinal: true);
            w.BeginPayload();
            w.WriteBits(8, 0x55);
        });
        reader.ReadPacket(packet, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var stats = context.BunchPayloadStats;
        Assert.That(stats.PartialErrorCount, Is.EqualTo(1));
    }

    [Test]
    public void Partial_PackageMapExports_ParsedBeforeAccumulation()
    {
        var context = CreateContext();
        AddOpenChannel(context, 13);
        var reader = new RawPacketReader();

        var p1 = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 13, bReliable: true, bPartial: true, bPartialInitial: true, bPartialFinal: false, bHasPackageMapExports: true);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteInt32(1);
            WriteInternalLoadObject(w, netGuid: 80, path: "/Game/Partial.Test_C", isExporting: true);
        });
        reader.ReadPacket(p1, 0, context.BunchPayloadPipeline.HandleBunchPayload);

        var p2 = BuildPacket(w =>
        {
            WriteBunchHeaderBits(w, chIndex: 13, bReliable: true, bPartial: true, bPartialInitial: false, bPartialFinal: true);
            w.BeginPayload();
            w.WriteBit(false);
            w.WriteBit(true);
            w.WriteIntPacked(0);
        });
        reader.ReadPacket(p2, 1, context.BunchPayloadPipeline.HandleBunchPayload);

        Assert.Multiple(() =>
        {
            Assert.That(context.NetGuidCache.PathByNetGuid, Does.ContainKey((uint)80));
            Assert.That(context.NetGuidCache.PathByNetGuid[80], Is.EqualTo("/Game/Partial.Test_C"));
            Assert.That(context.BunchPayloadStats.PackageMapExportBunchCount, Is.EqualTo(1));
            Assert.That(context.BunchPayloadStats.CompletedPartialBunchCount, Is.EqualTo(1));
        });
    }

    private static ReplayReaderContext CreateContext(IReplayEventSink? eventSink = null) =>
        new(new FBinaryArchive(ReadOnlyMemory<byte>.Empty), eventSink)
        {
            ReplayHeader = new ReplayHeader { NetworkVersion = Constants.ExpectedNetworkVersion },
        };

    private static void AddOpenChannel(ReplayReaderContext context, uint chIndex) =>
        context.ChannelStates[chIndex] = new ActorChannelState
        {
            ChannelIndex = chIndex,
            IsOpen = true,
            ActorNetGuid = new NetworkGuid(TestNetGuid),
            ArchetypeNetGuid = new NetworkGuid(TestArchetypeGuid),
        };

    private static NetFieldExportGroup CreateNetFieldExportGroup(
        string path,
        params (uint Handle, string Name)[] exports)
    {
        var length = exports.Length == 0 ? 0 : exports.Max(export => export.Handle) + 1;
        var netFields = new NetFieldExport?[length];
        foreach (var export in exports)
        {
            netFields[export.Handle] = new NetFieldExport
            {
                Handle = export.Handle,
                CompatibleChecksum = 0,
                Name = export.Name,
            };
        }

        return new NetFieldExportGroup
        {
            PathName = path,
            PathNameIndex = 42,
            NetFieldExportsLength = length,
            NetFieldExports = netFields,
        };
    }

    private static List<bool> BuildRepLayoutPropertyBits(uint handle, int bitCount, byte[] data)
    {
        var bits = new List<bool>();
        bits.Add(false);
        WriteIntPackedBits(bits, handle + 1);
        WriteIntPackedBits(bits, (uint)bitCount);
        WriteByteBits(bits, data, bitCount);
        WriteIntPackedBits(bits, 0);
        return bits;
    }

    private static void WriteIntPackedBits(List<bool> bits, uint value)
    {
        do
        {
            var nextByte = (byte)((value & 0x7F) << 1);
            value >>= 7;
            if (value != 0)
            {
                nextByte |= 1;
            }

            for (var i = 0; i < 8; i++)
            {
                bits.Add((nextByte & (1 << i)) != 0);
            }
        } while (value != 0);
    }

    private static void WriteByteBits(List<bool> bits, byte[] data, int bitCount)
    {
        for (var i = 0; i < bitCount; i++)
        {
            bits.Add((data[i >> 3] & (1 << (i & 7))) != 0);
        }
    }

    private static void WriteBunchHeaderBits(
        PacketBuilder w,
        uint chIndex = 0,
        bool bControl = false,
        bool bOpen = false,
        bool bClose = false,
        bool bReliable = false,
        bool bPartial = false,
        bool bPartialInitial = false,
        bool bPartialFinal = false,
        bool bHasPackageMapExports = false,
        bool bHasMustBeMappedGUIDs = false,
        ChannelCloseReason closeReason = ChannelCloseReason.Destroyed)
    {
        w.WriteBit(bControl);
        if (bControl)
        {
            w.WriteBit(bOpen);
            w.WriteBit(bClose);
            if (bClose)
            {
                w.WriteSerializedInt((uint)closeReason, (int)ChannelCloseReason.MAX);
            }
        }

        w.WriteBit(false);
        w.WriteBit(bReliable);
        w.WriteIntPacked(chIndex);
        w.WriteBit(bHasPackageMapExports);
        w.WriteBit(bHasMustBeMappedGUIDs);
        w.WriteBit(bPartial);

        if (bPartial)
        {
            w.WriteBit(bPartialInitial);
            w.WriteBit(bPartialFinal);
        }

        w.WriteBit(false);

        if (bReliable || bOpen)
        {
            w.WriteFName(1);
        }
    }

    private static void WriteInternalLoadObject(
        PacketBuilder w,
        uint netGuid,
        string? path = null,
        bool isExporting = false,
        uint checksum = 0)
    {
        w.WriteIntPacked(netGuid);
        if (netGuid == 0)
        {
            return;
        }

        if (netGuid == 1 || isExporting)
        {
            var flags = ExportFlags.None;
            if (path is not null)
            {
                flags |= ExportFlags.HasPath;
                if (checksum != 0)
                {
                    flags |= ExportFlags.HasNetworkChecksum;
                }
            }

            w.WriteByte((byte)flags);
        }

        if (path is not null)
        {
            w.WriteIntPacked(0);
            w.WriteFString(path);
            if (checksum != 0)
            {
                w.WriteUInt32(checksum);
            }
        }
    }

    private static void WriteSerializeNewActor(
        PacketBuilder w,
        uint actorNetGuid = TestNetGuid,
        bool includeLocation = false,
        bool includeRotation = false,
        bool includeScale = false,
        bool includeVelocity = false)
    {
        WriteInternalLoadObject(w, actorNetGuid);
        WriteInternalLoadObject(w, TestArchetypeGuid);
        WriteInternalLoadObject(w, TestLevelGuid);

        w.WriteBit(includeLocation);
        if (includeLocation)
        {
            w.WriteBit(true);
            w.WriteQuantizedVectorScaled(-1000.0, -300.0, 300.0, scaleFactor: 10, componentBitCount: 15);
        }

        w.WriteBit(includeRotation);
        if (includeRotation)
        {
            w.WriteCompressedShortRotatorComponent(0x4000);
            w.WriteCompressedShortRotatorComponent(0x2000);
            w.WriteCompressedShortRotatorComponent(0x1000);
        }

        w.WriteBit(includeScale);
        if (includeScale)
        {
            w.WriteBit(false);
            w.WriteFVectorDouble(1.5, 1.5, 1.5);
        }

        w.WriteBit(includeVelocity);
        if (includeVelocity)
        {
            w.WriteBit(true);
            w.WriteQuantizedVectorDouble(10.25, -0.5, 0.125);
        }
    }

    private static byte[] BuildPacket(params Action<PacketBuilder>[] writeBunches)
    {
        var totalBits = new List<bool>();
        foreach (var write in writeBunches)
        {
            var builder = new PacketBuilder();
            write(builder);

            totalBits.AddRange(builder.HeaderBits);

            if (builder.PayloadBits.Count > 0)
            {
                WriteSerializedIntBits(totalBits, (uint)builder.PayloadBits.Count, Constants.MaxPacketSizeInBits);
                totalBits.AddRange(builder.PayloadBits);
            }
            else
            {
                WriteSerializedIntBits(totalBits, 0, Constants.MaxPacketSizeInBits);
            }
        }

        var totalDataBits = totalBits.Count;
        var byteCount = (totalDataBits + 1 + 7) / 8;
        var packet = new byte[byteCount];
        for (var i = 0; i < totalBits.Count; i++)
        {
            if (totalBits[i])
            {
                packet[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        packet[totalDataBits >> 3] |= (byte)(1 << (totalDataBits & 7));
        return packet;
    }

    private static void WriteSerializedIntBits(List<bool> bits, uint value, int maxValue)
    {
        uint writtenValue = 0;
        for (uint mask = 1; writtenValue + mask < maxValue; mask <<= 1)
        {
            var bit = (value & mask) != 0;
            bits.Add(bit);
            if (bit)
            {
                writtenValue |= mask;
            }
        }
    }

    private sealed class PacketBuilder
    {
        private List<bool> _writeTarget;
        public List<bool> HeaderBits { get; } = [];
        public List<bool> PayloadBits { get; } = [];

        public PacketBuilder()
        {
            _writeTarget = HeaderBits;
        }

        public void BeginPayload() => _writeTarget = PayloadBits;

        public void WriteBit(bool value) => _writeTarget.Add(value);

        public void WriteBits(List<bool> bits) => _writeTarget.AddRange(bits);

        public void WriteByte(byte value)
        {
            for (var i = 0; i < 8; i++)
            {
                _writeTarget.Add((value & (1 << i)) != 0);
            }
        }

        public void WriteIntPacked(uint value)
        {
            do
            {
                var nextByte = (byte)((value & 0x7F) << 1);
                value >>= 7;
                if (value != 0)
                {
                    nextByte |= 1;
                }

                for (var i = 0; i < 8; i++)
                {
                    _writeTarget.Add((nextByte & (1 << i)) != 0);
                }
            } while (value != 0);
        }

        public void WriteSerializedInt(uint value, int maxValue)
        {
            uint writtenValue = 0;
            for (uint mask = 1; writtenValue + mask < maxValue; mask <<= 1)
            {
                var bit = (value & mask) != 0;
                _writeTarget.Add(bit);
                if (bit)
                {
                    writtenValue |= mask;
                }
            }
        }

        public void WriteQuantizedVectorScaled(
            double x,
            double y,
            double z,
            int scaleFactor,
            int componentBitCount)
        {
            var info = (uint)(componentBitCount | (1 << 6));
            WriteSerializedInt(info, 128);
            WriteSignedBits((long)Math.Round(x * scaleFactor), componentBitCount);
            WriteSignedBits((long)Math.Round(y * scaleFactor), componentBitCount);
            WriteSignedBits((long)Math.Round(z * scaleFactor), componentBitCount);
        }

        public void WriteQuantizedVectorDouble(double x, double y, double z)
        {
            WriteSerializedInt(1 << 6, 128);
            WriteDouble(x);
            WriteDouble(y);
            WriteDouble(z);
        }

        public void WriteFVectorDouble(double x, double y, double z)
        {
            WriteDouble(x);
            WriteDouble(y);
            WriteDouble(z);
        }

        private void WriteSignedBits(long value, int bitCount)
        {
            var mask = bitCount == 64 ? ulong.MaxValue : (1UL << bitCount) - 1;
            WriteBits(bitCount, (ulong)value & mask);
        }

        public void WriteCompressedShortRotatorComponent(ushort value)
        {
            var hasValue = value != 0;
            WriteBit(hasValue);
            if (hasValue)
            {
                WriteUInt16(value);
            }
        }

        private static int CeilLogTwo(int value)
        {
            var result = 0;
            var test = value - 1;
            while (test > 0)
            {
                test >>= 1;
                result++;
            }

            return result;
        }

        public void WriteInt32(int value)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buf, value);
            foreach (var b in buf)
            {
                WriteByte(b);
            }
        }

        public void WriteUInt16(ushort value)
        {
            Span<byte> buf = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
            foreach (var b in buf)
            {
                WriteByte(b);
            }
        }

        public void WriteUInt32(uint value)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
            foreach (var b in buf)
            {
                WriteByte(b);
            }
        }

        public void WriteUInt64(ulong value)
        {
            Span<byte> buf = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(buf, value);
            foreach (var b in buf)
            {
                WriteByte(b);
            }
        }

        public void WriteSingle(float value)
        {
            WriteUInt32(BitConverter.SingleToUInt32Bits(value));
        }

        public void WriteDouble(double value)
        {
            WriteUInt64(BitConverter.DoubleToUInt64Bits(value));
        }

        public void WriteBits(int count, ulong value)
        {
            for (var i = 0; i < count; i++)
            {
                _writeTarget.Add((value & (1UL << i)) != 0);
            }
        }

        public void WriteFString(string value)
        {
            var encoded = System.Text.Encoding.UTF8.GetBytes(value + '\0');
            WriteInt32(encoded.Length);
            foreach (var b in encoded)
            {
                WriteByte(b);
            }
        }

        public void WriteFName(uint nameIndex)
        {
            WriteBit(true);
            WriteIntPacked(nameIndex);
        }
    }

    private sealed class TestContentDescriptor : ExportGroupDescriptor<TestContentDescriptor>
    {
        public override string Path => TestPath;
        public override ExportCategory Categories => ExportCategory.Debug;
        public override ExportGroupKind Kind => ExportGroupKind.Actor;

        public int FieldA { get; set; }

        protected override void Configure()
        {
            AddProperty(x => x.FieldA).Decode(CountingFieldDecoder.Instance);
        }
    }

    private sealed class TestPlayerControllerDescriptor : ExportGroupDescriptor<TestPlayerControllerDescriptor>
    {
        public override string Path => TestPlayerControllerPath;
        public override ExportCategory Categories => ExportCategory.Movement;
        public override ExportGroupKind Kind => ExportGroupKind.PlayerController;

        protected override void Configure()
        {
        }
    }

    private sealed class ControllerNamedActorDescriptor : ExportGroupDescriptor<ControllerNamedActorDescriptor>
    {
        public override string Path => TestControllerNamedActorPath;
        public override ExportCategory Categories => ExportCategory.Debug;
        public override ExportGroupKind Kind => ExportGroupKind.Actor;

        protected override void Configure()
        {
        }
    }

    private sealed class CountingFieldDecoder : IFieldDecoder
    {
        public static readonly CountingFieldDecoder Instance = new();

        public static int DecodeCount { get; private set; }

        public static void Reset() => DecodeCount = 0;

        public void Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            _ = archive.ReadInt32();
            DecodeCount++;
        }
    }

    private sealed class CapturingReplayEventSink : IReplayEventSink
    {
        public List<ReplayEvent> Events { get; } = [];

        public void Emit(ReplayEvent replayEvent) => Events.Add(replayEvent);
    }
}
