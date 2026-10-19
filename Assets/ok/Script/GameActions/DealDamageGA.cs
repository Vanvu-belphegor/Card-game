using System.Collections.Generic;
using UnityEngine;

public class DealDamageGA : GameAction, IHaveCaster
{
    public int Amount { get; set; }
    public List<CombatantView> Targets { get; set; }
    public CombatantView Caster { get; private set; }

    // true khi Amount đã gồm Strength (tính ở Effect: cộng trước rồi mới nhân).
    // DamageSystem sẽ không cộng Strength lần nữa. Địch đánh Hero để false nên vẫn cộng như cũ.
    public bool IncludesStrength { get; set; }

    public DealDamageGA(int amount, List<CombatantView> targets, CombatantView caster)
    {
        Amount = amount;
        Targets = new(targets);
        Caster = caster;
    }
}
