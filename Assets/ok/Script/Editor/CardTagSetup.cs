#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Card Tags > Create Default Tags And Rules
// Tạo sẵn các asset nhãn và bảng TagRules mặc định. Chạy lại an toàn:
// nhãn đã có thì giữ, bảng luật đã có nội dung thì KHÔNG ghi đè.
public static class CardTagSetup
{
    private const string TagFolder = "Assets/ok/Data/Tags";
    private const string RulesPath = "Assets/Resources/TagRules.asset";

    private static readonly string[] TagNames =
        { "Fire", "Poison", "Armor", "Draw", "Exhaust", "Buff", "Debuff", "Burn", "Damage" };

    private static readonly (string key, string[] tags)[] DefaultRules =
    {
        ("Status:BURN",          new[] { "Burn", "Debuff", "Fire" }),
        ("Status:POISON",        new[] { "Poison", "Debuff" }),
        ("Status:ARMOR",         new[] { "Armor" }),
        ("Status:STRENGTH",      new[] { "Buff" }),
        ("Status:WEAK",          new[] { "Debuff" }),
        ("Status:VULNERABLE",    new[] { "Debuff" }),
        ("DealDamageEffect",     new[] { "Damage" }),
        ("DrawCardsEffect",      new[] { "Draw" }),
        ("MultiplyCardPowerEffect", new string[0]), // cố ý không có nhãn
    };

    [MenuItem("Tools/Card Tags/Create Default Tags And Rules")]
    public static void CreateDefaults()
    {
        EnsureFolder("Assets/ok/Data", "Tags");
        EnsureFolder("Assets", "Resources");

        var tags = new Dictionary<string, CardTag>();
        foreach (var name in TagNames) tags[name] = GetOrCreateTag(name);

        var rules = AssetDatabase.LoadAssetAtPath<TagRules>(RulesPath);
        if (rules == null)
        {
            rules = ScriptableObject.CreateInstance<TagRules>();
            AssetDatabase.CreateAsset(rules, RulesPath);
        }

        var so = new SerializedObject(rules);
        var arr = so.FindProperty("rules");
        if (arr.arraySize > 0)
        {
            Debug.Log("[CardTagSetup] TagRules đã có luật, giữ nguyên. Chỉ tạo thêm nhãn còn thiếu.");
        }
        else
        {
            arr.arraySize = DefaultRules.Length;
            for (int i = 0; i < DefaultRules.Length; i++)
            {
                var element = arr.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").stringValue = DefaultRules[i].key;
                var list = element.FindPropertyRelative("tags");
                list.arraySize = DefaultRules[i].tags.Length;
                for (int j = 0; j < DefaultRules[i].tags.Length; j++)
                    list.GetArrayElementAtIndex(j).objectReferenceValue = tags[DefaultRules[i].tags[j]];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[CardTagSetup] Đã tạo TagRules mặc định.");
        }

        EditorUtility.SetDirty(rules);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static CardTag GetOrCreateTag(string name)
    {
        string path = $"{TagFolder}/{name}.asset";
        var tag = AssetDatabase.LoadAssetAtPath<CardTag>(path);
        if (tag != null) return tag;
        tag = ScriptableObject.CreateInstance<CardTag>();
        AssetDatabase.CreateAsset(tag, path);
        return tag;
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
