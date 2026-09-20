using Replay.Encoding.Archives;
using Replay.Models.Descriptors;
using Replay.Models.Events;
using Replay.Models.Net;
using Replay.Models.Replay;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Tests.Parsing;

public class ReleaseAwareDescriptorTests
{
    private static readonly ReplayReleaseVersion Release1301 = new(13, 1);
    private static readonly ReplayReleaseVersion Release1305 = new(13, 5);

    [Test]
    public void ExportGroup_UsesReleaseSelectedTypedDescriptorAndExplicitHandles()
    {
        var catalog = new DescriptorCatalog();
        catalog.Add(new VersionedDefinition<ExportGroupDescriptor>(new LegacyTypedDescriptor())
            .From(Release1305, new CurrentTypedDescriptor()));
        var registry = new ExportBindingRegistry(catalog, releaseVersion: Release1305);

        registry.OnExportGroupAdded(CreateReplayGroup(
            TypedDescriptorPath,
            (0, "LegacyValue"),
            (1, "CurrentValue")));
        var bound = registry.GetBoundGroup(TypedDescriptorPath)!;

        Assert.Multiple(() =>
        {
            Assert.That(bound.SourceDescriptor, Is.TypeOf<CurrentTypedDescriptor>());
            Assert.That(bound.CreatePayloadInstance(), Is.TypeOf<CurrentTypedDescriptor>());
            Assert.That(bound.FieldsByHandle[0].Enabled, Is.False);
            Assert.That(bound.FieldsByHandle[1].Enabled, Is.True);
        });
    }

    [Test]
    public void FieldDecoder_SelectsByReleaseBeforeIdenticalPayloadIsDecoded()
    {
        var baseline = new TrackingFieldDecoder("baseline");
        var current = new TrackingFieldDecoder("13.05");
        var descriptor = new ExportGroupDescriptor(
            "/Game/VersionedDecoder.Test_C",
            fields:
            [
                new FieldDescriptor
                {
                    ExportName = "Value",
                    Decoder = baseline,
                    DecoderDefinition = new VersionedDefinition<IFieldDecoderDescriptor>(baseline)
                        .From(Release1305, current),
                },
            ]);
        var catalog = new DescriptorCatalog();
        catalog.Add(descriptor);
        var registry = new ExportBindingRegistry(catalog, releaseVersion: Release1305);
        registry.OnExportGroupAdded(CreateReplayGroup(descriptor.Path, (0, "Value")));
        var decoder = registry.GetBoundGroup(descriptor.Path)!.FieldsByHandle[0].Decoder!;
        var archive = new BitArchiveReader(ReadOnlyMemory<byte>.Empty, 0);
        var context = new FieldDecodeContext { ReplayReleaseVersion = Release1305 };

        _ = decoder.Decode(ref context, archive);

        Assert.Multiple(() =>
        {
            Assert.That(decoder, Is.SameAs(current));
            Assert.That(baseline.DecodeCount, Is.Zero);
            Assert.That(current.DecodeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void FieldDecoder_VersionedDefinitionWithoutReleaseFailsClearly()
    {
        var descriptor = CreateVersionedDecoderDescriptor();
        var catalog = new DescriptorCatalog();
        catalog.Add(descriptor);
        var registry = new ExportBindingRegistry(catalog);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.OnExportGroupAdded(CreateReplayGroup(descriptor.Path, (0, "Value"))));

        Assert.That(exception!.Message, Does.Contain("explicit replay release"));
    }

    [Test]
    public void ClassNetCache_SelectsRpcHandleAndDecoderByRelease()
    {
        const string path = "/Game/Versioned.Cache_C_ClassNetCache";
        var baselineDecoder = new TrackingRpcDecoder();
        var currentDecoder = new TrackingRpcDecoder();
        var decoderDefinition = new VersionedDefinition<IRpcDecoderDescriptor>(baselineDecoder)
            .From(Release1305, currentDecoder);
        var baseline = new ClassNetCacheDescriptor(path,
        [
            CreateRpc(handle: 0, decoderDefinition),
        ]);
        var current = new ClassNetCacheDescriptor(path,
        [
            CreateRpc(handle: 1, decoderDefinition),
        ]);
        var catalog = new DescriptorCatalog();
        catalog.Add(new VersionedDefinition<ClassNetCacheDescriptor>(baseline).From(Release1305, current));
        var registry = new ExportBindingRegistry(catalog, releaseVersion: Release1305);

        var bound = registry.GetBoundCache(path)!;

        Assert.Multiple(() =>
        {
            Assert.That(bound.SourceDescriptor, Is.SameAs(current));
            Assert.That(bound.FunctionsByHandle[0], Is.Null);
            Assert.That(bound.FunctionsByHandle[1].Decoder, Is.SameAs(currentDecoder));
        });
    }

    [Test]
    public void InheritedDescriptor_UsesReleaseSelectedBaseDescriptor()
    {
        const string basePath = "/Game/Versioned.Base_C";
        const string derivedPath = "/Game/Versioned.Derived_C";
        var baselineBase = Descriptor(basePath, "LegacyBase");
        var currentBase = Descriptor(basePath, "CurrentBase");
        var derived = new ExportGroupDescriptor(
            derivedPath,
            baseDescriptor: baselineBase,
            fields: [Field("Derived")]);
        var catalog = new DescriptorCatalog();
        catalog.Add(new VersionedDefinition<ExportGroupDescriptor>(baselineBase).From(Release1305, currentBase));
        catalog.Add(derived);
        var registry = new ExportBindingRegistry(catalog, releaseVersion: Release1305);

        registry.OnExportGroupAdded(CreateReplayGroup(derivedPath, (0, "CurrentBase"), (1, "Derived")));
        var bound = registry.GetBoundGroup(derivedPath)!;

        Assert.Multiple(() =>
        {
            Assert.That(bound.FieldsByHandle[0].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[0].Name, Is.EqualTo("CurrentBase"));
            Assert.That(bound.FieldsByHandle[1].Enabled, Is.True);
        });
    }

    [Test]
    public void InlineRpcParameters_UseReleaseSelectedDescriptor()
    {
        const string cachePath = "/Game/Inline.Cache_C_ClassNetCache";
        const string functionPath = "/Script/Test.Inline:Call";
        var baselineParameters = new ExportGroupDescriptor(
            functionPath,
            grammar: FieldStreamGrammar.FunctionParameters,
            fields: [FieldHandle(0, "Legacy")]);
        var currentParameters = new ExportGroupDescriptor(
            functionPath,
            grammar: FieldStreamGrammar.FunctionParameters,
            fields: [FieldHandle(1, "Current")]);
        var rpc = new RpcDescriptor
        {
            Name = "Call",
            FunctionExportPath = functionPath,
            Handle = 0,
            ParameterDescriptor = baselineParameters,
            ParameterDescriptorDefinition = new VersionedDefinition<ExportGroupDescriptor>(baselineParameters)
                .From(Release1305, currentParameters),
        };
        var catalog = new DescriptorCatalog();
        catalog.Add(new ClassNetCacheDescriptor(cachePath, [rpc]));
        var registry = new ExportBindingRegistry(catalog, releaseVersion: Release1305);

        var functionGroup = registry.GetBoundCache(cachePath)!.FunctionsByHandle[0].FunctionGroup!;

        Assert.Multiple(() =>
        {
            Assert.That(functionGroup.SourceDescriptor, Is.SameAs(currentParameters));
            Assert.That(functionGroup.FieldsByHandle[0].Enabled, Is.False);
            Assert.That(functionGroup.FieldsByHandle[1].Enabled, Is.True);
        });
    }

    [Test]
    public void VersionedFunctionDescriptor_StillSupportsDeferredRuntimeBinding()
    {
        const string cachePath = "/Game/Deferred.Cache_C_ClassNetCache";
        const string functionPath = "/Script/Test.Deferred:Call";
        var baselineFunction = Descriptor(functionPath, "Legacy");
        var currentFunction = Descriptor(functionPath, "Current");
        var catalog = new DescriptorCatalog();
        catalog.Add(new VersionedDefinition<ExportGroupDescriptor>(baselineFunction)
            .From(Release1305, currentFunction));
        catalog.Add(new ClassNetCacheDescriptor(cachePath,
        [
            new RpcDescriptor { Name = "Call", FunctionExportPath = functionPath },
        ]));
        var registry = new ExportBindingRegistry(catalog, releaseVersion: Release1305);
        registry.OnExportGroupAdded(CreateReplayGroup(cachePath, (0, "Call")));
        var function = registry.GetBoundCache(cachePath)!.FunctionsByHandle[0];

        Assert.That(function.FunctionGroup, Is.Null);

        registry.OnExportGroupAdded(CreateReplayGroup(functionPath, (0, "Current")));

        Assert.That(function.FunctionGroup, Is.Not.Null);
        Assert.That(function.FunctionGroup!.SourceDescriptor, Is.SameAs(currentFunction));
        Assert.That(function.FunctionGroup.FieldsByHandle[0].Enabled, Is.True);
    }

    [Test]
    public void VersionedDescriptor_PreservesAliasAndParseProfileSelection()
    {
        const string aliasPath = "/Game/Versioned.TypedAlias_C";
        var catalog = new DescriptorCatalog
        {
            PathAliasProvider = new TestAliasProvider(aliasPath, TypedDescriptorPath),
        };
        catalog.Add(new VersionedDefinition<ExportGroupDescriptor>(new LegacyTypedDescriptor())
            .From(Release1305, new CurrentTypedDescriptor()));
        var profile = new ParseProfile
        {
            EnabledCategories = ExportCategory.Ability,
            IncludedPaths = [TypedDescriptorPath],
            IncludedFields = [nameof(CurrentTypedDescriptor.Value)],
        };
        var registry = new ExportBindingRegistry(catalog, profile, Release1305);

        registry.OnExportGroupAdded(CreateReplayGroup(aliasPath, (1, "CurrentValue")));
        var bound = registry.GetBoundGroup(aliasPath)!;

        Assert.Multiple(() =>
        {
            Assert.That(bound.SourceDescriptor, Is.TypeOf<CurrentTypedDescriptor>());
            Assert.That(bound.Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[1].Enabled, Is.True);
        });
    }

    private const string TypedDescriptorPath = "/Game/Versioned.Typed_C";

    private static ExportGroupDescriptor CreateVersionedDecoderDescriptor()
    {
        var baseline = new TrackingFieldDecoder("baseline");
        return new ExportGroupDescriptor(
            "/Game/MissingRelease.Test_C",
            fields:
            [
                new FieldDescriptor
                {
                    ExportName = "Value",
                    Decoder = baseline,
                    DecoderDefinition = new VersionedDefinition<IFieldDecoderDescriptor>(baseline)
                        .From(Release1305, new TrackingFieldDecoder("13.05")),
                },
            ]);
    }

    private static ExportGroupDescriptor Descriptor(string path, string fieldName) =>
        new(path, fields: [Field(fieldName)]);

    private static FieldDescriptor Field(string name) => new()
    {
        ExportName = name,
        PropertyName = name,
        Decoder = PrimitiveDecoders.Int32,
    };

    private static FieldDescriptor FieldHandle(uint handle, string name) => new()
    {
        Handle = handle,
        PropertyName = name,
        Decoder = PrimitiveDecoders.Int32,
    };

    private static RpcDescriptor CreateRpc(
        uint handle,
        VersionedDefinition<IRpcDecoderDescriptor> decoderDefinition) => new()
        {
            Name = "Call",
            FunctionExportPath = "/Script/Test.Versioned:Call",
            Handle = handle,
            Decoder = decoderDefinition.Baseline,
            DecoderDefinition = decoderDefinition,
        };

    private static NetFieldExportGroup CreateReplayGroup(
        string path,
        params (uint Handle, string Name)[] exports)
    {
        var length = exports.Length == 0 ? 0 : checked((int)exports.Max(item => item.Handle) + 1);
        var fields = new NetFieldExport?[length];
        foreach (var (handle, name) in exports)
        {
            fields[handle] = new NetFieldExport { Handle = handle, Name = name, CompatibleChecksum = 0 };
        }

        return new NetFieldExportGroup
        {
            PathName = path,
            PathNameIndex = 1,
            NetFieldExports = fields,
        };
    }

    private sealed class LegacyTypedDescriptor : ExportGroupDescriptor<LegacyTypedDescriptor>
    {
        public override string Path => TypedDescriptorPath;
        public override ExportCategory Categories => ExportCategory.Ability;
        public int Value { get; set; }

        protected override void Configure() => AddPropertyHandle(0, x => x.Value).Int32();
    }

    private sealed class CurrentTypedDescriptor : ExportGroupDescriptor<CurrentTypedDescriptor>
    {
        public override string Path => TypedDescriptorPath;
        public override ExportCategory Categories => ExportCategory.Ability;
        public int Value { get; set; }

        protected override void Configure() => AddPropertyHandle(1, x => x.Value).Int32();
    }

    private sealed class TrackingFieldDecoder(string name) : IFieldDecoder
    {
        public string Name { get; } = name;
        public int DecodeCount { get; private set; }

        public DecodedFieldValue Decode(ref FieldDecodeContext context, FBitArchive archive)
        {
            DecodeCount++;
            return DecodedFieldValue.FromInt32(0);
        }
    }

    private sealed class TrackingRpcDecoder : IRpcDecoder
    {
        public DecodedPayloadResult Decode(ref FieldDecodeContext context, FBitArchive archive) =>
            DecodedPayloadResult.Empty;
    }

    private sealed class TestAliasProvider(string aliasPath, string descriptorPath) : IReplayPathAliasProvider
    {
        public string? GetAlternatePath(string path) => path switch
        {
            var value when value == aliasPath => descriptorPath,
            var value when value == descriptorPath => aliasPath,
            _ => null,
        };
    }
}
