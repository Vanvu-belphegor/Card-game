using System;
using UnityEngine;

// Bộ lọc bài dùng chung cho mọi effect "tìm bài". Chỉnh trong Inspector.
// Bộ lọc để trống = khớp mọi lá.
[Serializable]
public class CardFilter
{
    [Tooltip("None = không lọc theo loại")]
    [SerializeField] private CardType type = CardType.None;

    [Tooltip("Lá phải có ÍT NHẤT MỘT trong các nhãn này (để trống = bỏ qua)")]
    [SerializeField] private CardTags anyOf = CardTags.None;

    [Tooltip("Lá phải có TẤT CẢ các nhãn này")]
    [SerializeField] private CardTags allOf = CardTags.None;

    [Tooltip("Lá KHÔNG được có nhãn nào trong số này")]
    [SerializeField] private CardTags noneOf = CardTags.None;

    public bool Matches(Card card)
    {
        if (card == null) return false;
        if (type != CardType.None && card.Type != type) return false;
        if (anyOf != CardTags.None && (card.Tags & anyOf) == 0) return false;
        if ((card.Tags & allOf) != allOf) return false;
        if ((card.Tags & noneOf) != 0) return false;
        return true;
    }
}
