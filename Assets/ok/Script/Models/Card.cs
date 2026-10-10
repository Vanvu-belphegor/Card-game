using System.Collections.Generic;
using UnityEngine;

public class Card
{
    public string Title => data.name;
    // Thêm "(x2)" màu vàng khi lá bài đang được nhân chỉ số
    public string Description => data.Description + (Multiplier > 1 ? $" <color=#FFD54A>(x{Multiplier})</color>" : "");
    public Sprite Image => data.Image;
    public Effect ManualTargetEffect => data.ManualTargetEffect;
    public List<AutoTargetEffect> OtherEffects => data.OtherEffects;
    public int Mana { get; private set; }

    // Phân loại: Type cố định, Tags là bản sao runtime nên có thể thêm/bớt trong trận
    public CardType Type => data.Type;
    public CardTags Tags { get; private set; }
    public bool HasTag(CardTags tag) => (Tags & tag) == tag;
    public void AddTag(CardTags tag) => Tags |= tag;
    public void RemoveTag(CardTags tag) => Tags &= ~tag;

    // Hệ số nhân chỉ số của lần chơi kế tiếp (lưu trên Card runtime, KHÔNG sửa CardData)
    public int Multiplier { get; private set; } = 1;
    public void MultiplyPower(int factor) => Multiplier *= Mathf.Max(1, factor);
    public void ResetMultiplier() => Multiplier = 1;

    private readonly CardData data;
    public Card(CardData cardData)
    {
        data = cardData;
        Mana = cardData.Mana;
        Tags = cardData.Tags;
    }
}
