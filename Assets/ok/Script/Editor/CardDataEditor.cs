#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Hiện bên dưới Inspector của CardData: nhãn CUỐI CÙNG của lá này và từng nguồn của nó.
// Chỉ để xem (không sửa ở đây). Muốn sửa: Manual Tags / Excluded Tags ở trên, hoặc bảng TagRules.
[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var data = (CardData)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Nhãn của lá này (chỉ xem)", EditorStyles.boldLabel);

        var rules = TagRules.Active;
        if (rules == null)
        {
            EditorGUILayout.HelpBox(
                "Chưa có Assets/Resources/TagRules.asset. Chạy Tools > Card Tags > Create Default Tags And Rules.",
                MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var final = TagRules.ResolveTags(data);
            EditorGUILayout.LabelField("Kết quả cuối", Join(final), EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Từ đâu ra:", EditorStyles.miniBoldLabel);

            foreach (var effect in EffectsOf(data))
            {
                string key = effect.TagKey;
                if (rules.TryGetTags(key, out var tags))
                    EditorGUILayout.LabelField("Effect " + key, Join(tags), EditorStyles.wordWrappedLabel);
                else
                    EditorGUILayout.HelpBox($"Effect '{key}' chưa có luật trong TagRules.", MessageType.Warning);
            }

            EditorGUILayout.LabelField("Thêm tay", Join(data.ManualTags), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Loại trừ", Join(data.ExcludedTags), EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.HelpBox("Sửa bảng TagRules xong thì bấm vào Inspector này để làm mới.", MessageType.None);
    }

    private static IEnumerable<Effect> EffectsOf(CardData data)
    {
        if (data.ManualTargetEffect != null) yield return data.ManualTargetEffect;
        if (data.OtherEffects == null) yield break;
        foreach (var other in data.OtherEffects)
            if (other?.Effect != null) yield return other.Effect;
    }

    public static string Join(IEnumerable<CardTag> tags)
    {
        if (tags == null) return "(không có)";
        var names = tags.Where(t => t != null).Select(t => t.name).OrderBy(n => n).ToList();
        return names.Count == 0 ? "(không có)" : string.Join(", ", names);
    }
}
#endif
