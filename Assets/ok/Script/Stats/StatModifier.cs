public enum StatModifierType
{
    Flat,        // +3
    Multiplier   // x2 (x0.75 để giảm 25%)
}

public enum ModifierLifetime
{
    UntilConsumed,   // dùng một lần (lúc chơi bài) rồi bị xóa; cuối lượt chưa dùng cũng mất
    UntilEndOfTurn,  // có tác dụng nhiều lần trong lượt, cuối lượt bị xóa
    Permanent        // không tự hết hạn; chỉ mất khi bị xóa theo nguồn (RemoveBySource)
}

// Một thay đổi chỉ số. Bất biến sau khi tạo.
// SourceId nên là ID DUY NHẤT cho mỗi lần phát sinh (không chỉ là tên lá/status),
// để xóa đúng nguồn mà không xóa nhầm modifier của lần áp dụng khác.
public sealed class StatModifier
{
    public StatKey Scope { get; }
    public StatModifierType Type { get; }
    public float Value { get; }
    public ModifierLifetime Lifetime { get; }
    public string SourceId { get; }

    public StatModifier(StatKey scope, StatModifierType type, float value,
                        ModifierLifetime lifetime, string sourceId = null)
    {
        Scope = scope;
        Type = type;
        Value = value;
        Lifetime = lifetime;
        SourceId = sourceId;
    }

    public static StatModifier Flat(StatKey scope, float value, ModifierLifetime lifetime, string sourceId = null)
        => new(scope, StatModifierType.Flat, value, lifetime, sourceId);

    public static StatModifier Multiply(StatKey scope, float value, ModifierLifetime lifetime, string sourceId = null)
        => new(scope, StatModifierType.Multiplier, value, lifetime, sourceId);

    public bool AppliesTo(StatKey key) => key.IsCoveredBy(Scope);
}
