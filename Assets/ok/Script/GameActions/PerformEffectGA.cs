using System.Collections.Generic;
using UnityEngine;

public class PerformEffectGA : GameAction
{
    public Effect Effect { get; set; }
    public List<CombatantView> Targets { get; set; }
    public int Multiplier { get; private set; }
    public PerformEffectGA(Effect effect, List<CombatantView> targets, int multiplier = 1)
    {
        Effect = effect;
        Targets = targets == null ? null : new(targets);
        Multiplier = multiplier;
    }
}
