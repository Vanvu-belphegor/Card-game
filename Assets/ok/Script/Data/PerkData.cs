using SerializeReferenceEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Perk")]
public class PerkData : ScriptableObject
{
    [field: SerializeField] public Sprite Image { get; private set; }
    [field: SerializeReference, SR] public PerkCondition PerkCondition { get; private set; }
    [field: SerializeReference, SR] public AutoTargetEffect AutoTargetEffect { get; private set; }
    [field: SerializeField] public bool UseAutoTarget { get; private set; } = true;
    [field: SerializeField] public bool UseActionCasterAsTarget { get; private set; } = false;

    [Tooltip("Thêm CHỦ perk (Enemy sở hữu) vào danh sách mục tiêu. Dùng cho perk kiểu 'Enemy tự nhận giáp'")]
    [field: SerializeField] public bool UseOwnerAsTarget { get; private set; } = false;
}