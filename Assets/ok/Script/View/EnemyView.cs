using TMPro;
using UnityEngine;

public class EnemyView : CombatantView
{
    [SerializeField] private TMP_Text attackText;
    public int AttackPower { get; set; }
    public void Setup(EnemyData enemyData)
    {
        AttackPower = enemyData.AttackPower;
        UpdateAttackText();
        SetupBase(enemyData.Health, enemyData.Image);

        // Perk khởi đầu của Enemy này
        if (enemyData.Perks != null && PerkSystem.Instance != null)
            foreach (PerkData perkData in enemyData.Perks)
                if (perkData != null) PerkSystem.Instance.AddPerk(new Perk(perkData, this));
    }
    private void UpdateAttackText()
    {
        attackText.text = "ATK: " + AttackPower;
    }
}