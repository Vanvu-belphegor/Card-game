using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Enemy")]
public class EnemyData : ScriptableObject
{
    [field: SerializeField] public Sprite Image { get; private set; }
    [field: SerializeField] public int Health { get; private set; }
    [field: SerializeField] public int AttackPower { get; private set; }

    [Tooltip("Perk Enemy này có từ đầu trận")]
    [field: SerializeField] public List<PerkData> Perks { get; private set; } = new();
}