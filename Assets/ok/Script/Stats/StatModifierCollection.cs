using System.Collections.Generic;

// Danh sách modifier runtime của MỘT chủ sở hữu (Card, sau này là Character...).
// Là lớp C# thường, không phải MonoBehaviour/singleton.
public sealed class StatModifierCollection
{
    private readonly List<StatModifier> items = new();
    public IReadOnlyList<StatModifier> Items => items;
    public int Count => items.Count;

    public void Add(StatModifier modifier)
    {
        if (modifier != null) items.Add(modifier);
    }

    public int RemoveBySource(string sourceId)
        => items.RemoveAll(m => m.SourceId == sourceId);

    // Gọi khi hết lượt: xóa mọi modifier tạm (UntilConsumed + UntilEndOfTurn), giữ Permanent
    public int ClearOnTurnEnd()
        => items.RemoveAll(m => m.Lifetime != ModifierLifetime.Permanent);

    // Gọi ĐÚNG MỘT LẦN khi chơi bài: trả về ảnh chụp các modifier đang có để áp dụng,
    // đồng thời xóa loại dùng một lần. Nhờ vậy nhiều Effect của cùng lá bài đều
    // thấy cùng một bộ modifier mà không ai tiêu thụ trùng.
    public List<StatModifier> ConsumeForPlay()
    {
        List<StatModifier> snapshot = new(items);
        items.RemoveAll(m => m.Lifetime == ModifierLifetime.UntilConsumed);
        return snapshot;
    }

    // Tính thử trên các modifier hiện có (không tiêu thụ) - dùng cho preview/UI
    public int Calculate(int baseValue, StatKey key)
        => StatCalculator.Calculate(baseValue, key, items);

    // Tích các hệ số nhân áp dụng cho MỌI chỉ số (phạm vi All). Dùng để hiện "(x2)".
    public float GetUniversalMultiplier()
    {
        float result = 1f;
        foreach (StatModifier m in items)
            if (m.Scope.Kind == StatKind.All && m.Type == StatModifierType.Multiplier)
                result *= m.Value;
        return result;
    }
}
