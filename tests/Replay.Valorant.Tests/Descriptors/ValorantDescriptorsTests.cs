using Replay.Models.Descriptors;
using Replay.Valorant.Descriptors;

namespace Replay.Valorant.Tests.Descriptors;

public class ValorantDescriptorsTests
{
    [Test]
    public void CreateCatalog_IncludesPlayableAgentDescriptors()
    {
        string[] expectedAgentPaths =
        [
            "/Game/Characters/Aggrobot/Aggrobot_PC.Aggrobot_PC_C",
            "/Game/Characters/BountyHunter/BountyHunter_PC.BountyHunter_PC_C",
            "/Game/Characters/Breach/Breach_PC.Breach_PC_C",
            "/Game/Characters/Cable/Cable_PC.Cable_PC_C",
            "/Game/Characters/Cashew/Cashew_PC.Cashew_PC_C",
            "/Game/Characters/Clay/Clay_PC.Clay_PC_C",
            "/Game/Characters/Deadeye/Deadeye_PC.Deadeye_PC_C",
            "/Game/Characters/Grenadier/Grenadier_PC.Grenadier_PC_C",
            "/Game/Characters/Guide/Guide_PC.Guide_PC_C",
            "/Game/Characters/Gumshoe/Gumshoe_PC.Gumshoe_PC_C",
            "/Game/Characters/Hunter/Hunter_PC.Hunter_PC_C",
            "/Game/Characters/Iris/Iris_PC.Iris_PC_C",
            "/Game/Characters/Killjoy/Killjoy_PC.Killjoy_PC_C",
            "/Game/Characters/Mage/Mage_PC.Mage_PC_C",
            "/Game/Characters/Nox/Nox_PC.Nox_PC_C",
            "/Game/Characters/Pandemic/Pandemic_PC.Pandemic_PC_C",
            "/Game/Characters/Phoenix/Phoenix_PC.Phoenix_PC_C",
            "/Game/Characters/Pine/Pine_PC.Pine_PC_C",
            "/Game/Characters/Rift/Rift_PC.Rift_PC_C",
            "/Game/Characters/Sarge/Sarge_PC.Sarge_PC_C",
            "/Game/Characters/Sequoia/Sequoia_PC.Sequoia_PC_C",
            "/Game/Characters/Smonk/Smonk_PC.Smonk_PC_C",
            "/Game/Characters/Sprinter/Sprinter_PC.Sprinter_PC_C",
            "/Game/Characters/Stealth/Stealth_PC.Stealth_PC_C",
            "/Game/Characters/Terra/Terra_PC.Terra_PC_C",
            "/Game/Characters/Thorne/Thorne_PC.Thorne_PC_C",
            "/Game/Characters/Vampire/Vampire_PC.Vampire_PC_C",
            "/Game/Characters/Wraith/Wraith_PC.Wraith_PC_C",
            "/Game/Characters/Wushu/Wushu_PC.Wushu_PC_C",
        ];

        var actualAgentPaths = ValorantDescriptors.CreateCatalog()
            .ExportGroupDescriptors
            .Where(descriptor => descriptor.Categories.HasFlag(ExportCategory.Agent))
            .Select(descriptor => descriptor.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.That(actualAgentPaths, Is.EqualTo(expectedAgentPaths));
    }
}