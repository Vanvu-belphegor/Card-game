using System.Collections.Generic;
using UnityEngine;

public class MatchSetupSystem : MonoBehaviour
{
    [SerializeField] private HeroData heroData;
    [Tooltip("Cũ: nên dùng danh sách Perks trong HeroData")]
    [SerializeField] private PerkData perkData;
    [SerializeField] private List<EnemyData> enemyDatas;

    /// <summary>
    /// Setup các giá trị ban đầu cho các hệ thống khi bắt đầu trận đấu,
    /// bao gồm thiết lập Hero, Enemy, Card và Perk. Sau đó thực hiện hành
    /// động rút 5 lá bài từ bộ bài của Hero.
    /// </summary>
    private void Start()
    {
        HeroSystem.Instance.Setup(heroData);
        EnemySystem.Instance.Setup(enemyDatas);
        CardSystem.Instance.Setup(heroData.Deck);
        // Perk đầu trận của Hero: danh sách trong HeroData. Ô perkData cũ vẫn dùng được (không thêm trùng).
        if (heroData.Perks != null)
            foreach (PerkData p in heroData.Perks)
                if (p != null) PerkSystem.Instance.AddPerk(new Perk(p));
        if (perkData != null && (heroData.Perks == null || !heroData.Perks.Contains(perkData)))
            PerkSystem.Instance.AddPerk(new Perk(perkData));
        DrawCardsGA drawCardsGA = new(5);
        ActionSystem.Instance.Perform(drawCardsGA);

    }



}