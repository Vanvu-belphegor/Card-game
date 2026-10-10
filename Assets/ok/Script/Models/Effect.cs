using System.Collections.Generic;

[System.Serializable]
public abstract class Effect
{
    public abstract GameAction GetGameAction(List<CombatantView> targets, CombatantView caster);

    // Bản có hệ số nhân. Mặc định BỎ QUA multiplier;
    // effect nào có "chỉ số" (damage, stack, số lá rút...) thì override.
    public virtual GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, int multiplier)
    {
        return GetGameAction(targets, caster);
    }
}
