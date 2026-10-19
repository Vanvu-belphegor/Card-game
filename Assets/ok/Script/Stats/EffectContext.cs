using System;
using System.Collections.Generic;

// Ngữ cảnh tính chỉ số cho một lần thực thi Effect. Thay cho tham số `int multiplier`.
// Hiện chỉ chứa danh sách modifier; sau này có thể thêm nguồn lá bài, nhân vật...
public sealed class EffectContext
{
    public static readonly EffectContext Empty = new(null);

    private readonly IReadOnlyList<StatModifier> modifiers;
    public IReadOnlyList<StatModifier> Modifiers => modifiers;

    public EffectContext(IReadOnlyList<StatModifier> modifiers)
    {
        this.modifiers = modifiers ?? Array.Empty<StatModifier>();
    }

    // Context mới = modifier hiện có + modifier bổ sung (vd: Strength của người gây ra hiệu ứng)
    public EffectContext With(IEnumerable<StatModifier> extra)
    {
        List<StatModifier> all = new(modifiers);
        if (extra != null) all.AddRange(extra);
        return new EffectContext(all);
    }

    // Giá trị cuối của một chỉ số, từ giá trị gốc cấu hình trong Inspector
    public int Calc(StatKey key, int baseValue)
        => StatCalculator.Calculate(baseValue, key, modifiers);
}
