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

    // Khóa để tra bảng TagRules. Mặc định là tên class.
    // Effect có nhiều biến thể (vd status) thì override để phân biệt.
    public virtual string TagKey => GetType().Name;
}
