using Replay.Encoding.Net;
using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Unreal.Tests.Parsing;

public class ExportBindingRegistryTests
{
    [Test]
    public void OnExportGroupAdded_BindsByNameToDescriptor()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new TestDescriptor()));
        var replayGroup = CreateReplayGroup("/Game/Test.Test_C", exports:
        [
            (0u, "FieldA"),
            (1u, "FieldB"),
        ]);

        registry.OnExportGroupAdded(replayGroup);

        var bound = registry.GetBoundGroup("/Game/Test.Test_C");
        Assert.That(bound, Is.Not.Null);
        Assert.That(bound!.Enabled, Is.True);
    }

    [Test]
    public void OnExportGroupAdded_ResolvesFieldDescriptorByExportName()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new TestDescriptor()));
        var replayGroup = CreateReplayGroup("/Game/Test.Test_C", exports:
        [
            (0u, "FieldA"),
            (1u, "FieldB"),
            (2u, "bIsActive"),
        ]);

        registry.OnExportGroupAdded(replayGroup);

        var bound = registry.GetBoundGroup("/Game/Test.Test_C")!;
        Assert.Multiple(() =>
        {
            Assert.That(bound.FieldsByHandle[0].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[0].Name, Is.EqualTo("FieldA"));
            Assert.That(bound.FieldsByHandle[0].ExportName, Is.EqualTo("FieldA"));
            Assert.That(bound.FieldsByHandle[1].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[1].Name, Is.EqualTo("FieldB"));
            Assert.That(bound.FieldsByHandle[2].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[2].Name, Is.EqualTo("IsActive"));
            Assert.That(bound.FieldsByHandle[2].ExportName, Is.EqualTo("bIsActive"));
        });
    }

    [Test]
    public void DescriptorWithNoDecoder_IsDisabled()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new TestDescriptor()));
        var replayGroup = CreateReplayGroup("/Game/Test.Test_C", exports:
        [
            (0u, "FieldA"),
            (1u, "NoDecoderField"),
        ]);

        registry.OnExportGroupAdded(replayGroup);

        var bound = registry.GetBoundGroup("/Game/Test.Test_C")!;
        Assert.That(bound.FieldsByHandle[0].Enabled, Is.True);
        Assert.That(bound.FieldsByHandle[1].Enabled, Is.False);
    }

    [Test]
    public void UnknownField_DefaultsToDisabled()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new TestDescriptor()));
        var replayGroup = CreateReplayGroup("/Game/Test.Test_C", exports:
        [
            (0u, "FieldA"),
            (1u, "FieldB"),
            (2u, "UnknownField"),
        ]);

        registry.OnExportGroupAdded(replayGroup);

        var bound = registry.GetBoundGroup("/Game/Test.Test_C")!;
        Assert.Multiple(() =>
        {
            Assert.That(bound.FieldsByHandle[0].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[1].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[2].Enabled, Is.False);
            Assert.That(bound.FieldsByHandle[2].Name, Is.EqualTo("UnknownField"));
        });
    }

    [Test]
    public void ExplicitHandle_BindsCorrectly()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new ExplicitHandleDescriptor()));
        var replayGroup = CreateReplayGroup("/Game/Explicit.Test_C", exports:
        [
            (0u, "FieldZero"),
            (1u, "FieldOne"),
            (2u, "FieldTwo"),
        ]);

        registry.OnExportGroupAdded(replayGroup);

        var bound = registry.GetBoundGroup("/Game/Explicit.Test_C")!;
        Assert.Multiple(() =>
        {
            Assert.That(bound.FieldsByHandle[1].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[1].Name, Is.EqualTo("FieldOne"));
            Assert.That(bound.FieldsByHandle[0].Enabled, Is.False);
            Assert.That(bound.FieldsByHandle[2].Enabled, Is.False);
        });
    }

    [Test]
    public void BasePath_FieldsFlattenIntoDerived()
    {
        var registry = CreateRegistry(catalog =>
        {
            catalog.Add(new BaseDescriptor());
            catalog.Add(new DerivedDescriptor());
        });
        var replayGroup = CreateReplayGroup("/Game/Derived.Derived_C", exports:
        [
            (0u, "BaseField"),
            (1u, "DerivedField"),
        ]);

        registry.OnExportGroupAdded(replayGroup);

        var bound = registry.GetBoundGroup("/Game/Derived.Derived_C")!;
        Assert.Multiple(() =>
        {
            Assert.That(bound.FieldsByHandle[0].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[0].Name, Is.EqualTo("BaseField"));
            Assert.That(bound.FieldsByHandle[1].Enabled, Is.True);
            Assert.That(bound.FieldsByHandle[1].Name, Is.EqualTo("DerivedField"));
        });
    }

    [Test]
    public void ClassNetCache_BindsFunctionByName()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new CacheDescriptor()));
        var cacheGroup = CreateReplayGroup("/Game/Cache.SomeCache_C_ClassNetCache", exports:
        [
            (0u, "SomeFunction"),
        ]);

        registry.OnExportGroupAdded(cacheGroup);

        var bound = registry.GetBoundCache("/Game/Cache.SomeCache_C_ClassNetCache")!;
        Assert.Multiple(() =>
        {
            Assert.That(bound.Enabled, Is.True);
            Assert.That(bound.FunctionsByHandle[0].Name, Is.EqualTo("SomeFunction"));
            Assert.That(bound.FunctionsByHandle[0].Enabled, Is.True);
            Assert.That(bound.FunctionsByHandle[0].FunctionExportPath,
                Is.EqualTo("/Script/GameModule.SomeClass:SomeFunction"));
        });
    }

    [Test]
    public void ParseProfileMinimal_DisablesGroupAndFieldsAtBindTime()
    {
        var catalog = new DescriptorCatalog();
        catalog.Add(new TestDescriptor());
        var registry = new ExportBindingRegistry(catalog, ParseProfile.Minimal);
        var replayGroup = CreateReplayGroup("/Game/Test.Test_C", exports:
        [
            (0u, "FieldA"),
        ]);

        registry.OnExportGroupAdded(replayGroup);

        var bound = registry.GetBoundGroup("/Game/Test.Test_C")!;
        Assert.Multiple(() =>
        {
            Assert.That(bound.Enabled, Is.False);
            Assert.That(bound.FieldsByHandle[0].Enabled, Is.False);
            Assert.That(bound.FieldsByHandle[0].Decoder, Is.Null);
        });
    }

    [Test]
    public void RpcFunctionGroup_BindsWhenFunctionExportArrivesAfterCache()
    {
        var registry = CreateRegistry(catalog =>
        {
            catalog.Add(new SomeFunctionDescriptor());
            catalog.Add(new CacheDescriptorWithoutInlineFields());
        });
        var cacheGroup = CreateReplayGroup("/Game/Cache.SomeCache_C_ClassNetCache", exports:
        [
            (0u, "SomeFunction"),
        ]);
        var functionGroup = CreateReplayGroup("/Script/GameModule.SomeClass:SomeFunction", exports:
        [
            (0u, "Param1"),
        ]);

        registry.OnExportGroupAdded(cacheGroup);
        var boundCache = registry.GetBoundCache("/Game/Cache.SomeCache_C_ClassNetCache")!;
        Assert.That(boundCache.FunctionsByHandle[0].FunctionGroup, Is.Null);

        registry.OnExportGroupAdded(functionGroup);

        Assert.That(boundCache.FunctionsByHandle[0].FunctionGroup, Is.Not.Null);
        Assert.That(boundCache.FunctionsByHandle[0].FunctionGroup!.FieldsByHandle[0].Enabled, Is.True);
    }

    [Test]
    public void OnExportGroupChanged_RebindsExistingGroupWhenFieldsArriveLater()
    {
        var catalog = new DescriptorCatalog();
        catalog.Add(new TestDescriptor());
        var registry = new ExportBindingRegistry(catalog);
        var emptyGroup = new NetFieldExportGroup
        {
            PathName = "/Game/Test.Test_C",
            PathNameIndex = 7,
            NetFieldExportsLength = 1,
            NetFieldExports = new NetFieldExport?[1],
        };

        registry.OnExportGroupChanged(emptyGroup);
        var initiallyBound = registry.GetBoundGroup("/Game/Test.Test_C")!;
        Assert.That(initiallyBound.FieldsByHandle[0].Enabled, Is.False);

        registry.OnExportGroupChanged(CreateReplayGroup("/Game/Test.Test_C", exports:
        [
            (0u, "FieldA"),
        ]));

        var rebound = registry.GetBoundGroup("/Game/Test.Test_C")!;
        Assert.That(rebound.FieldsByHandle[0].Enabled, Is.True);
    }

    [Test]
    public void OnExportGroupChanged_BindsKnownGroupWithoutCacheScan()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new TestDescriptor()));

        registry.OnExportGroupChanged(CreateReplayGroup("/Game/Test.Test_C", exports:
        [
            (0u, "FieldA"),
            (1u, "FieldB"),
        ]));

        Assert.That(registry.GetBoundGroup("/Game/Test.Test_C"), Is.Not.Null);
    }

    [Test]
    public void GetBoundGroupByIndex_ResolvesPathIndex()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new TestDescriptor()));
        var replayGroup = CreateReplayGroup("/Game/Test.Test_C", pathNameIndex: 42, exports:
        [
            (0u, "FieldA"),
        ]);

        registry.OnExportGroupAdded(replayGroup);

        var result = registry.GetBoundGroupByIndex(42);
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void Clear_RemovesAllBindings()
    {
        var registry = CreateRegistry(catalog => catalog.Add(new TestDescriptor()));
        registry.OnExportGroupAdded(CreateReplayGroup("/Game/Test.Test_C", exports:
        [
            (0u, "FieldA"),
        ]));

        registry.Clear();

        Assert.That(registry.GetBoundGroup("/Game/Test.Test_C"), Is.Null);
    }

    private static ExportBindingRegistry CreateRegistry(Action<DescriptorCatalog> configure)
    {
        var catalog = new DescriptorCatalog();
        configure(catalog);
        return new ExportBindingRegistry(catalog);
    }

    private static NetFieldExportGroup CreateReplayGroup(
        string path,
        uint pathNameIndex = 7,
        params (uint Handle, string Name)[] exports)
    {
        var maxHandle = exports.Length > 0 ? exports.Max(e => e.Handle) + 1 : 0u;
        var netFields = new NetFieldExport?[Math.Max(maxHandle, (uint)exports.Length)];
        foreach (var (handle, name) in exports)
        {
            netFields[handle] = new NetFieldExport
            {
                Handle = handle,
                CompatibleChecksum = 0,
                Name = name,
            };
        }

        return new NetFieldExportGroup
        {
            PathName = path,
            PathNameIndex = pathNameIndex,
            NetFieldExportsLength = (uint)netFields.Length,
            NetFieldExports = netFields,
        };
    }

    private sealed class TestDescriptor : ExportGroupDescriptor<TestDescriptor>
    {
        public override string Path => "/Game/Test.Test_C";
        public override ExportCategory Categories => ExportCategory.Ability;
        public override ExportGroupKind Kind => ExportGroupKind.Actor;

        public int FieldA { get; set; }
        public uint FieldB { get; set; }
        public bool IsActive { get; set; }
        public int NoDecoderField { get; set; }

        protected override void Configure()
        {
            AddProperty(x => x.FieldA).Int32();
            AddProperty(x => x.FieldB).ObjectNetGuid();
            AddProperty("bIsActive", x => x.IsActive).Bool();
            AddProperty(x => x.NoDecoderField);
        }
    }

    private sealed class ExplicitHandleDescriptor : ExportGroupDescriptor<ExplicitHandleDescriptor>
    {
        public override string Path => "/Game/Explicit.Test_C";
        public override ExportCategory Categories => ExportCategory.Movement;
        public override ExportGroupKind Kind => ExportGroupKind.Actor;

        public float FieldOne { get; set; }

        protected override void Configure()
        {
            AddPropertyHandle(1, x => x.FieldOne).Float();
        }
    }

    private sealed class BaseDescriptor : ExportGroupDescriptor<BaseDescriptor>
    {
        public override string Path => "/Game/Base.Base_C";
        public override ExportCategory Categories => ExportCategory.Ability;
        public override ExportGroupKind Kind => ExportGroupKind.Actor;

        public int BaseField { get; set; }

        protected override void Configure()
        {
            AddProperty(x => x.BaseField).Int32();
        }
    }

    private sealed class DerivedDescriptor : ExportGroupDescriptor<DerivedDescriptor>
    {
        public override string Path => "/Game/Derived.Derived_C";
        public override ExportCategory Categories => ExportCategory.Ability;
        public override ExportGroupKind Kind => ExportGroupKind.Actor;
        public override string BasePath => "/Game/Base.Base_C";

        public bool DerivedField { get; set; }

        protected override void Configure()
        {
            AddProperty(x => x.DerivedField).Bool();
        }
    }

    private sealed class CacheDescriptor : ClassNetCacheDescriptor<CacheDescriptor>
    {
        public override string Path => "/Game/Cache.SomeCache_C_ClassNetCache";

        protected override void Configure()
        {
            AddFunction(
                    "SomeFunction",
                    "/Script/GameModule.SomeClass:SomeFunction",
                    ExportCategory.Movement)
                .AddField("Param1", "Param1", ExportCategory.Movement);
        }
    }

    private sealed class CacheDescriptorWithoutInlineFields : ClassNetCacheDescriptor<CacheDescriptorWithoutInlineFields>
    {
        public override string Path => "/Game/Cache.SomeCache_C_ClassNetCache";

        protected override void Configure()
        {
            AddFunction(
                "SomeFunction",
                "/Script/GameModule.SomeClass:SomeFunction",
                ExportCategory.Movement);
        }
    }

    private sealed class SomeFunctionDescriptor : ExportGroupDescriptor<SomeFunctionDescriptor>
    {
        public override string Path => "/Script/GameModule.SomeClass:SomeFunction";
        public override ExportCategory Categories => ExportCategory.Movement;
        public override ExportGroupKind Kind => ExportGroupKind.ClassNetCache;
        public override FieldStreamGrammar Grammar => FieldStreamGrammar.FunctionParameters;

        public float Param1 { get; set; }

        protected override void Configure()
        {
            AddProperty(x => x.Param1).Float();
        }
    }
}
