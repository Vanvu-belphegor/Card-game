using System.Collections.Generic;
using UnityEngine;

public class MatchSetupSystem : MonoBehaviour
{
    [SerializeField] private HeroData heroData;
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
        PerkSystem.Instance.AddPerk(new Perk(perkData));
        DrawCardsGA drawCardsGA = new(5);
        ActionSystem.Instance.Perform(drawCardsGA);

    }



}