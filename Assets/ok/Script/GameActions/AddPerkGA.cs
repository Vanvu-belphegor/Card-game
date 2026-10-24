using System.Collections.Generic;

public class AddPerkGA : GameAction
{
    public PerkData PerkData { get; private set; }
    public List<CombatantView> Targets { get; private set; }
    public bool AllowDuplicate { get; private set; }

    public AddPerkGA(PerkData perkData, List<CombatantView> targets, bool allowDuplicate)
    {
        PerkData = perkData;
        Targets = targets;
        AllowDuplicate = allowDuplicate;
    }
}
