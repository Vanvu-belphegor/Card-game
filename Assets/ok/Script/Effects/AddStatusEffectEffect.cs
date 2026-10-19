using System.Collections.Generic;
using UnityEngine;

public class AddStatusEffectEffect : Effect
{
    [SerializeField] private StatusEffectType statusEffectType;
    [SerializeField] private int stackCount;
    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return GetGameAction(targets, caster, EffectContext.Empty);
    }
    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, EffectContext context)
    {
        // stackCount là giá trị GỐC trong Inspector; giá trị cuối tính ở đây
        return new AddStatusEffectGA(statusEffectType, context.Calc(StatKey.Stacks(statusEffectType), stackCount), targets);
    }

    public override bool TryGetStat(out StatKey key, out int baseValue)
    {
        key = StatKey.Stacks(statusEffectType);
        baseValue = stackCount;
        return true;
    }

    // Cùng một class nhưng Burn/Poison/Armor... cần nhãn khác nhau -> khóa theo loại status
    public override string TagKey => "Status:" + statusEffectType;
}
