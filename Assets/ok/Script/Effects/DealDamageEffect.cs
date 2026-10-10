using System.Collections.Generic;
using UnityEngine;

public class DealDamageEffect : Effect
{
    [SerializeField] private int damageAmount;
    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        DealDamageGA dealDamageGA = new(damageAmount, targets, caster);
        return dealDamageGA;
    }
    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int multiplier)
    {
        return new DealDamageGA(damageAmount * multiplier, targets, caster);
    }
}
