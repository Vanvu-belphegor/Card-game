using System.Collections.Generic;
using UnityEngine;

// Thêm một perk cho các mục tiêu (Hero và/hoặc Enemy). Mỗi mục tiêu nhận một perk riêng, thuộc về chính nó.
public class AddPerkEffect : Effect
{
    [SerializeField] private PerkData perkData;

    [Tooltip("Tắt: mục tiêu đã có perk này rồi thì không nhận thêm. Bật: nhận thêm bản nữa (kích hoạt nhiều lần)")]
    [SerializeField] private bool allowDuplicate = false;

    public override GameAction GetGameAction(List<CombatantView> targets, CombatantView caster)
    {
        return new AddPerkGA(perkData, targets, allowDuplicate);
    }
}
