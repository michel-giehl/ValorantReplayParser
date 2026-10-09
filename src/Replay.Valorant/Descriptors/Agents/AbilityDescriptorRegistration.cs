using Replay.Models.Descriptors;
using Replay.Unreal.Parsing;

namespace Replay.Valorant.Descriptors.Agents;

/// <summary>Shared wire registration helpers; ability paths live in each agent's catalog.</summary>
internal static class AbilityDescriptorRegistration
{
    public static AbilityActorDescriptor Actor(string path, string[] fields) =>
        new(path, fields.ToHashSet(StringComparer.Ordinal));

    public static ClassNetCacheDescriptor Rpc(string path, string name, uint handle) =>
        new(path + "_ClassNetCache", [new RpcDescriptor
        {
            Name = name, FunctionExportPath = path + ":" + name, Handle = handle,
            Categories = ExportCategory.Ability, Decoder = ValorantPayloadDecoders.NoParametersRpc,
        }]);

    public static ClassNetCacheDescriptor ObjectRpc(string path, string name, uint handle, string parameter) =>
        new(path + "_ClassNetCache", [new RpcDescriptor
        {
            Name = name, FunctionExportPath = path + ":" + name, Handle = handle,
            Categories = ExportCategory.Ability, ParameterDescriptor = new AbilityObjectParameters(path + ":" + name, parameter),
        }]);

    public static IReadOnlyList<ClassNetCacheDescriptor> MergeClassNetCaches(IReadOnlyList<ClassNetCacheDescriptor> caches) =>
        caches.GroupBy(c => c.Path, StringComparer.Ordinal)
            .Select(g => new ClassNetCacheDescriptor(g.Key, g.SelectMany(c => c.FunctionFields).ToArray())).ToArray();
}
