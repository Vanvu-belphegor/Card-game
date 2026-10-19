using System.Collections.Generic;
using UnityEngine;

public class DealDamageEffect : Effect
{
    [SerializeField] private int damageAmount;
    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return GetGameAction(targets, caster, EffectContext.Empty);
    }
    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, EffectContext context)
    {
        // (Base + Strength) x hệ số nhân: Strength là Flat nên được cộng trước khi nhân
        EffectContext full = context.With(StatSources.FromCaster(caster));
        int amount = full.Calc(StatKey.Damage, damageAmount);
        return new DealDamageGA(amount, targets, caster) { IncludesStrength = true };
    }

    public override bool TryGetStat(out StatKey key, out int baseValue)
    {
        key = StatKey.Damage;
        baseValue = damageAmount;
        return true;
    }
}
