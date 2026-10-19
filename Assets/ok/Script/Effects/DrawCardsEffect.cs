using System.Collections.Generic;
using UnityEngine;

public class DrawCardsEffect : Effect
{
    [SerializeField] private int drawAmount;
    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return GetGameAction(targets, caster, EffectContext.Empty);
    }
    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, EffectContext context)
    {
        return new DrawCardsGA(context.Calc(StatKey.DrawCount, drawAmount));
    }

    public override bool TryGetStat(out StatKey key, out int baseValue)
    {
        key = StatKey.DrawCount;
        baseValue = drawAmount;
        return true;
    }
}
