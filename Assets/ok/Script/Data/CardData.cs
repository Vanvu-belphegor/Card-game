using SerializeReferenceEditor;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Card")]
public class CardData : ScriptableObject
{
    [field: SerializeField] public string Description { get; private set; }
    [field: SerializeField] public int Mana { get; private set; }
    [field: SerializeField] public Sprite Image { get; private set; }
    [field: SerializeField] public CardType Type { get; private set; }

    [Tooltip("Nhãn thêm bằng tay. Nhãn từ effect đã được thêm TỰ ĐỘNG theo bảng TagRules.")]
    [field: SerializeField] public List<CardTag> ManualTags { get; private set; } = new();

    [Tooltip("Nhãn muốn BỎ khỏi lá này dù effect hay danh sách tay có thêm vào.")]
    [field: SerializeField] public List<CardTag> ExcludedTags { get; private set; } = new();

    [field: SerializeReference, SR] public Effect ManualTargetEffect { get; private set; } = null;
    [field: SerializeField] public List<AutoTargetEffect> OtherEffects { get; private set; }
}
