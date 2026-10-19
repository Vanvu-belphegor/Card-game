using System.Collections.Generic;

[System.Serializable]
public abstract class Effect
{
    public abstract GameAction GetGameAction(List<CombatantView> targets, CombatantView caster);

    // Bản có ngữ cảnh chỉ số (modifier). Mặc định BỎ QUA context;
    // effect nào có "chỉ số" (damage, stack, số lá rút...) thì override và gọi ctx.Calc(...).
    public virtual GameAction GetGameAction(List<CombatantView> targets, CombatantView caster, EffectContext context)
    {
        return GetGameAction(targets, caster);
    }

    // Effect có chỉ số thì khai báo (StatKey + giá trị gốc) để mô tả lá bài thay được {damage}, {draw}...
    public virtual bool TryGetStat(out StatKey key, out int baseValue)
    {
        key = default;
        baseValue = 0;
        return false;
    }

    // Khóa để tra bảng TagRules. Mặc định là tên class.
    // Effect có nhiều biến thể (vd status) thì override để phân biệt.
    public virtual string TagKey => GetType().Name;
}
