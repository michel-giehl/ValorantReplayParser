using Replay.Models.Descriptors;

namespace Replay.Valorant.Descriptors;

public static class ValorantDescriptors
{
    public static DescriptorCatalog CreateCatalog()
    {
        var catalog = new DescriptorCatalog();

        catalog.Add(new AresAbilitySystemComponentDescriptor());
        catalog.Add(new AresAttributeSetDescriptor());
        catalog.Add(new RemoteCharacterUpdateDescriptor());
        catalog.Add(new BaseReplayControllerDescriptor());
        catalog.Add(new TripWireAbilityDescriptor());
        catalog.Add(new SmokeAbilityDescriptor());

        catalog.Add(new BaseReplayControllerClassNetCacheDescriptor());
        catalog.Add(new AresAbilitySystemComponentClassNetCacheDescriptor());

        return catalog;
    }
}
