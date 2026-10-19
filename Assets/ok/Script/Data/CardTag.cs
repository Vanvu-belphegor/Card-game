using UnityEngine;

// Mỗi nhãn là một asset: Assets > Create > Data > Card Tag.
// Thêm nhãn mới không cần sửa code. Các nhãn so sánh bằng THAM CHIẾU asset (không phải chuỗi).
[CreateAssetMenu(menuName = "Data/Card Tag")]
public class CardTag : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private Color color = Color.white;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Color Color => color;
}
