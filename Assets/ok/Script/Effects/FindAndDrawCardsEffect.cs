using System.Collections.Generic;
using UnityEngine;

// Tìm lá bài khớp bộ lọc (loại / nhãn / lá cụ thể) trong bộ bài và/hoặc bài bỏ rồi đưa lên tay.
public class FindAndDrawCardsEffect : Effect
{
    [SerializeField] private CardFilter filter;

    [Tooltip("Vùng được tìm: tick Draw Pile (bộ bài) và/hoặc Discard Pile (bài bỏ)")]
    [SerializeField] private CardZoneMask zones = CardZoneMask.DrawPile | CardZoneMask.DiscardPile;

    [Tooltip("Random: tự bốc ngẫu nhiên. Player Choice: hiện danh sách cho người chơi chọn")]
    [SerializeField] private CardSelectionMode mode = CardSelectionMode.Random;

    [Tooltip("Số lá tối đa lấy lên tay. 1 = một lá, 0 = tất cả lá khớp")]
    [SerializeField] private int maxCards = 1;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new FindAndDrawCardsGA(filter, zones, maxCards, mode);
    }
}
