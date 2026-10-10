using System.Collections.Generic;
using UnityEngine;

// Nhân chỉ số của các lá KHÁC trong tay (khớp bộ lọc), cho lần chơi kế tiếp của chúng
public class MultiplyCardPowerEffect : Effect
{
    [SerializeField] private int factor = 2;
    [SerializeField] private CardFilter filter;
    [Tooltip("Số lá tối đa bị nhân (chọn ngẫu nhiên trong số khớp). 0 = tất cả")]
    [SerializeField] private int maxCards = 1;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new MultiplyCardPowerGA(factor, filter, maxCards);
    }
}
