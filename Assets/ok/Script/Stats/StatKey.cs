using System;

public enum StatKind
{
    All,            // chỉ dùng làm "phạm vi" của modifier: áp dụng cho mọi chỉ số
    Damage,
    DrawCount,
    StatusStacks    // số stack của một status (xem StatKey.Status)
}

// Khóa định danh một chỉ số. Gộp StatKind với StatusEffectType để KHÔNG phải
// tạo thêm một danh sách chỉ số song song với enum StatusEffectType.
public readonly struct StatKey : IEquatable<StatKey>
{
    public StatKind Kind { get; }
    public StatusEffectType? Status { get; }

    private StatKey(StatKind kind, StatusEffectType? status)
    {
        Kind = kind;
        Status = status;
    }

    public static readonly StatKey All = new(StatKind.All, null);
    public static readonly StatKey Damage = new(StatKind.Damage, null);
    public static readonly StatKey DrawCount = new(StatKind.DrawCount, null);
    public static StatKey Stacks(StatusEffectType type) => new(StatKind.StatusStacks, type);

    // Modifier có phạm vi `scope` thì tác động lên chỉ số này khi:
    // scope là All, hoặc scope trùng đúng chỉ số này.
    public bool IsCoveredBy(StatKey scope) => scope.Kind == StatKind.All || Equals(scope);

    // Tên dùng trong mô tả lá bài: {damage}, {draw}, {burn}, {poison}, {armor}, {strength}...
    public string Token
    {
        get
        {
            switch (Kind)
            {
                case StatKind.Damage: return "damage";
                case StatKind.DrawCount: return "draw";
                case StatKind.StatusStacks: return Status.Value.ToString().ToLowerInvariant();
                default: return "all";
            }
        }
    }

    public bool Equals(StatKey other) => Kind == other.Kind && Status == other.Status;
    public override bool Equals(object obj) => obj is StatKey other && Equals(other);
    public override int GetHashCode() => ((int)Kind * 397) ^ (Status.HasValue ? (int)Status.Value + 1 : 0);
    public override string ToString() => Status.HasValue ? $"{Kind}:{Status.Value}" : Kind.ToString();
}
