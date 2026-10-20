using System.Collections.Generic;
using UnityEngine;

public class Card
{
    public string Title => data.name;
    // Mô tả gốc người dùng nhập trong CardData (có thể chứa {damage}, {draw}, {burn}...)
    public string RawDescription => data.Description;

    // Mô tả đã thay số: {damage} -> "5" hoặc "(5+5)*5 = 50" tùy modifier và Strength hiện tại
    public string Description =>
        CardDescription.Build(this, HeroSystem.Instance != null ? HeroSystem.Instance.HeroView : null);
    public Sprite Image => data.Image;
    public Effect ManualTargetEffect => data.ManualTargetEffect;
    public List<AutoTargetEffect> OtherEffects => data.OtherEffects;
    public int Mana { get; private set; }

    // Phân loại: Type cố định. Nhãn là bản runtime nên có thể thêm/bớt trong trận.
    public CardType Type => data.Type;

    // Nhãn lúc chạy = luật theo effect + nhãn tick tay - nhãn loại trừ (xem TagRules.ResolveTags)
    private readonly HashSet<CardTag> tags;
    public IReadOnlyCollection<CardTag> Tags => tags;
    public bool HasTag(CardTag tag) => tag != null && tags.Contains(tag);
    public void AddTag(CardTag tag) { if (tag != null) tags.Add(tag); }
    public void RemoveTag(CardTag tag) { if (tag != null) tags.Remove(tag); }

    // Modifier chỉ số runtime của lá bài này (lưu trên Card, KHÔNG sửa CardData).
    // Hai lá cùng CardData có hai collection riêng nên không ảnh hưởng nhau.
    public StatModifierCollection Modifiers { get; } = new();

    // Asset CardData gốc của lá (dùng để so khớp bộ lọc "Cards")
    public CardData Data => data;

    private readonly CardData data;
    public Card(CardData cardData)
    {
        data = cardData;
        Mana = cardData.Mana;
        tags = TagRules.ResolveTags(cardData);
    }
}
