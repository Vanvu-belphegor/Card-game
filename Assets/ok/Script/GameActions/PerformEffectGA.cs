using System.Collections.Generic;
using UnityEngine;

public class PerformEffectGA : GameAction
{
    public Effect Effect { get; set; }
    public List<CombatantView> Targets { get; set; }
    public EffectContext Context { get; private set; }
    public PerformEffectGA(Effect effect, List<CombatantView> targets, EffectContext context = null)
    {
        Effect = effect;
        Targets = targets == null ? null : new(targets);
        Context = context ?? EffectContext.Empty;
    }
}
