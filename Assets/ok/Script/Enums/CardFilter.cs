using System;
using System.Collections.Generic;
using UnityEngine;

// Bộ lọc bài dùng chung cho mọi effect "tìm bài". Chỉnh trong Inspector.
// Bộ lọc để trống = khớp mọi lá. Ô nhãn bỏ trống (None) được bỏ qua.
[Serializable]
public class CardFilter
{
    [Tooltip("None = không lọc theo loại")]
    [SerializeField] private CardType type = CardType.None;

    [Tooltip("Lá phải có ÍT NHẤT MỘT trong các nhãn này (để trống = bỏ qua)")]
    [SerializeField] private List<CardTag> anyOf = new();

    [Tooltip("Lá phải có TẤT CẢ các nhãn này")]
    [SerializeField] private List<CardTag> allOf = new();

    [Tooltip("Lá KHÔNG được có nhãn nào trong số này")]
    [SerializeField] private List<CardTag> noneOf = new();

    [Tooltip("Kéo thả asset CardData vào đây. Khớp MỘT trong các lá này. Để trống = bỏ qua")]
    [SerializeField] private List<CardData> cards = new();

    // Unity cần constructor rỗng để dựng bộ lọc từ Inspector
    public CardFilter() { }

    // Dựng bộ lọc từ code (cửa sổ Find Cards dùng đúng logic Matches này nên kết quả y hệt trong game)
    public CardFilter(CardType type, IEnumerable<CardTag> anyOf, IEnumerable<CardTag> allOf, IEnumerable<CardTag> noneOf)
    {
        this.type = type;
        this.anyOf = new List<CardTag>(anyOf);
        this.allOf = new List<CardTag>(allOf);
        this.noneOf = new List<CardTag>(noneOf);
    }

    public bool Matches(Card card)
    {
        if (card == null) return false;
        if (type != CardType.None && card.Type != type) return false;

        if (!MatchesName(card)) return false;

        if (HasAny(card, noneOf)) return false;

        foreach (var tag in allOf)
            if (tag != null && !card.HasTag(tag)) return false;

        if (CountSet(anyOf) > 0 && !HasAny(card, anyOf)) return false;

        return true;
    }

    private bool MatchesName(Card card)
    {
        bool any = false;
        foreach (CardData data in cards)
        {
            if (data == null) continue;
            any = true;
            if (card.Data == data) return true;
        }
        return !any; // không kéo lá nào vào = không lọc theo lá
    }

    private static bool HasAny(Card card, List<CardTag> list)
    {
        foreach (var tag in list)
            if (tag != null && card.HasTag(tag)) return true;
        return false;
    }

    private static int CountSet(List<CardTag> list)
    {
        int n = 0;
        foreach (var tag in list) if (tag != null) n++;
        return n;
    }
}
