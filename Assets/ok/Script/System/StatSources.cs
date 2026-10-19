using System.Collections.Generic;

// Nơi duy nhất quy định "status nào của người gây hiệu ứng biến thành modifier nào".
// Cả lúc tạo GameAction (DealDamageEffect) lẫn lúc hiện mô tả lá bài (CardDescription)
// đều dùng chung hàm này nên số hiển thị luôn khớp số gây ra.
public static class StatSources
{
    public static List<StatModifier> FromCaster(CombatantView caster)
    {
        List<StatModifier> list = new();
        if (caster == null) return list;

        // Strength: +1 Damage mỗi stack. Là Flat nên được CỘNG trước khi nhân.
        int strength = caster.GetStatusEffectStacks(StatusEffectType.STRENGTH);
        if (strength != 0)
            list.Add(StatModifier.Flat(StatKey.Damage, strength, ModifierLifetime.Permanent, "Status:STRENGTH"));

        return list;
    }
}
