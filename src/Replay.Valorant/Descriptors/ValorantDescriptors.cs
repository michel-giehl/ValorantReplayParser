using Replay.Models.Descriptors;
using Replay.Valorant.Combat;
using Replay.Valorant.Descriptors.Agents.Aggrobot;
using Replay.Valorant.Descriptors.Agents;
using Replay.Valorant.Descriptors.Agents.BountyHunter;
using Replay.Valorant.Descriptors.Agents.Breach;
using Replay.Valorant.Descriptors.Agents.Cable;
using Replay.Valorant.Descriptors.Agents.Cashew;
using Replay.Valorant.Descriptors.Agents.Clay;
using Replay.Valorant.Descriptors.Agents.Deadeye;
using Replay.Valorant.Descriptors.Agents.Grenadier;
using Replay.Valorant.Descriptors.Agents.Guide;
using Replay.Valorant.Descriptors.Agents.Gumshoe;
using Replay.Valorant.Descriptors.Agents.Hunter;
using Replay.Valorant.Descriptors.Agents.Iris;
using Replay.Valorant.Descriptors.Agents.Killjoy;
using Replay.Valorant.Descriptors.Agents.Mage;
using Replay.Valorant.Descriptors.Agents.Nox;
using Replay.Valorant.Descriptors.Agents.Pandemic;
using Replay.Valorant.Descriptors.Agents.Phoenix;
using Replay.Valorant.Descriptors.Agents.Pine;
using Replay.Valorant.Descriptors.Agents.Rift;
using Replay.Valorant.Descriptors.Agents.Sarge;
using Replay.Valorant.Descriptors.Agents.Sequoia;
using Replay.Valorant.Descriptors.Agents.Smonk;
using Replay.Valorant.Descriptors.Agents.Sprinter;
using Replay.Valorant.Descriptors.Agents.Stealth;
using Replay.Valorant.Descriptors.Agents.Terra;
using Replay.Valorant.Descriptors.Agents.Thorne;
using Replay.Valorant.Descriptors.Agents.Vampire;
using Replay.Valorant.Descriptors.Agents.Wraith;
using Replay.Valorant.Descriptors.Agents.Wushu;
using Replay.Valorant.Descriptors.Effects.Replay;
using Replay.Valorant.GameState;

namespace Replay.Valorant.Descriptors;

public static class ValorantDescriptors
{
    public static DescriptorCatalog CreateCatalog()
    {
        var catalog = new DescriptorCatalog();

        catalog.Add(AggrobotDescriptors.CreateDescriptors());
        catalog.Add(BountyHunterDescriptors.CreateDescriptors());
        catalog.Add(BreachDescriptors.CreateDescriptors());
        catalog.Add(CableDescriptors.CreateDescriptors());
        catalog.Add(CashewDescriptors.CreateDescriptors());
        catalog.Add(ClayDescriptors.CreateDescriptors());
        catalog.Add(DeadeyeDescriptors.CreateDescriptors());
        catalog.Add(GrenadierDescriptors.CreateDescriptors());
        catalog.Add(GuideDescriptors.CreateDescriptors());
        catalog.Add(GumshoeDescriptors.CreateDescriptors());
        catalog.Add(HunterDescriptors.CreateDescriptors());
        catalog.Add(IrisDescriptors.CreateDescriptors());
        catalog.Add(KilljoyDescriptors.CreateDescriptors());
        catalog.Add(MageDescriptors.CreateDescriptors());
        catalog.Add(MageDescriptors.CreateClassNetCacheDescriptors());
        catalog.Add(NoxDescriptors.CreateDescriptors());
        catalog.Add(PandemicDescriptors.CreateDescriptors());
        catalog.Add(PandemicDescriptors.CreateClassNetCacheDescriptors());
        catalog.Add(PhoenixDescriptors.CreateDescriptors());
        catalog.Add(PineDescriptors.CreateDescriptors());
        catalog.Add(RiftDescriptors.CreateDescriptors());
        catalog.Add(SargeDescriptors.CreateDescriptors());
        catalog.Add(SequoiaDescriptors.CreateDescriptors());
        catalog.Add(SmonkDescriptors.CreateDescriptors());
        catalog.Add(SprinterDescriptors.CreateDescriptors());
        catalog.Add(StealthDescriptors.CreateDescriptors());
        catalog.Add(TerraDescriptors.CreateDescriptors());
        catalog.Add(ThorneDescriptors.CreateDescriptors());
        catalog.Add(VampireDescriptors.CreateDescriptors());
        catalog.Add(WraithDescriptors.CreateDescriptors());
        catalog.Add(WushuDescriptors.CreateDescriptors());

        catalog.Add(new AresAbilitySystemComponentDescriptor());
        catalog.Add(new AresAttributeSetDescriptor());
        catalog.Add(new AmmoComponentDescriptor());
        catalog.Add(new AresInventoryDescriptor());
        catalog.Add(new AttachedDamageSectionComponentDescriptor());
        catalog.Add(new BombCombatReportComponentDescriptor());
        catalog.Add(new BombGameStateDescriptor());
        catalog.Add(new BombPlayerStateDescriptor());
        catalog.Add(new ChildDamageSectionComponentDescriptor());
        catalog.Add(new ChildRegionDamageSectionComponentDescriptor());
        catalog.Add(new EquippableStateMachineComponentDescriptor());
        catalog.Add(new RemoteCharacterUpdateDescriptor());
        catalog.Add(new BaseReplayPlayerState());
        catalog.Add(new BaseReplayControllerDescriptor());
        catalog.Add(new BaseReplayControllerClassNetCacheDescriptor());
        catalog.Add(new BombGameStateClassNetCacheDescriptor());
        catalog.Add(new AresAbilitySystemComponentClassNetCacheDescriptor());
        catalog.Add(new ChildDamageSectionClassNetCacheDescriptor());
        catalog.Add(new AttachedDamageSectionClassNetCacheDescriptor());
        catalog.Add(new ArmorDamageSectionClassNetCacheDescriptor());
        catalog.Add(new ReplayEffectComponentClassNetCacheDescriptor());
        catalog.Add(new DamageableComponentClassNetCacheDescriptor());
        catalog.Add(AgentClassNetCacheDescriptors.Create(
            catalog.ExportGroupDescriptors.Where(descriptor => descriptor.Categories.HasFlag(ExportCategory.Agent))));

        return catalog;
    }
}
