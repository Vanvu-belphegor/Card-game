using System;
using System.Collections.Generic;
using UnityEngine;

// Bảng luật: "effect này thì lá bài tự có những nhãn này".
// Chỉ có MỘT asset, đặt tại Assets/Resources/TagRules.asset (tên phải đúng "TagRules").
// Sửa bảng trong Inspector là mọi lá dùng effect đó đổi theo, không cần sửa từng lá.
//
// Khóa (key) lấy từ Effect.TagKey:
//   - mặc định là tên class, vd "DealDamageEffect", "DrawCardsEffect"
//   - AddStatusEffectEffect trả về "Status:" + loại status, vd "Status:BURN"
// Luật có danh sách nhãn TRỐNG = "effect này cố ý không có nhãn" (để không bị cảnh báo).
[CreateAssetMenu(menuName = "Data/Tag Rules")]
public class TagRules : ScriptableObject
{
    [Serializable]
    public class Rule
    {
        [TagKey] public string key; // chọn từ danh sách thả xuống, không cần gõ
        public List<CardTag> tags = new();
    }

    [SerializeField] private List<Rule> rules = new();

    private Dictionary<string, List<CardTag>> lookup;

    private void OnEnable() => lookup = null;
    private void OnValidate() => lookup = null; // sửa trong Inspector thì dựng lại bảng tra

    public bool TryGetTags(string key, out List<CardTag> tags)
    {
        if (lookup == null) Build();
        return lookup.TryGetValue(key ?? "", out tags);
    }

    private void Build()
    {
        lookup = new Dictionary<string, List<CardTag>>();
        foreach (var rule in rules)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.key)) continue;
            if (!lookup.TryGetValue(rule.key, out var list))
            {
                list = new List<CardTag>();
                lookup[rule.key] = list;
            }
            list.AddRange(rule.tags); // trùng khóa thì gộp
        }
    }

    // ── Truy cập toàn cục ────────────────────────────────────────────
    private static TagRules active;
    private static bool warnedMissingAsset;
    private static readonly HashSet<string> warnedKeys = new();

    public static TagRules Active
    {
        get
        {
            if (active == null) active = Resources.Load<TagRules>("TagRules");
            if (active == null && !warnedMissingAsset)
            {
                warnedMissingAsset = true;
                Debug.LogWarning("[TagRules] Không tìm thấy Assets/Resources/TagRules.asset. " +
                                 "Chạy menu Tools > Card Tags > Create Default Tags And Rules.");
            }
            return active;
        }
    }

    // Nhãn cuối của lá = nhãn từ luật (theo từng effect) + nhãn tick tay - nhãn loại trừ.
    // Tính lúc tạo Card khi chạy game, KHÔNG ghi ngược vào CardData.
    public static HashSet<CardTag> ResolveTags(CardData data)
    {
        var result = new HashSet<CardTag>();
        if (data == null) return result;

        AddFromEffect(data.ManualTargetEffect, result);
        if (data.OtherEffects != null)
            foreach (var other in data.OtherEffects)
                AddFromEffect(other?.Effect, result);

        if (data.ManualTags != null)
            foreach (var tag in data.ManualTags)
                if (tag != null) result.Add(tag);

        if (data.ExcludedTags != null)
            foreach (var tag in data.ExcludedTags)
                if (tag != null) result.Remove(tag);

        return result;
    }

    private static void AddFromEffect(Effect effect, HashSet<CardTag> result)
    {
        if (effect == null) return;
        var rules = Active;
        if (rules == null) return;

        string key = effect.TagKey;
        if (rules.TryGetTags(key, out var tags))
        {
            foreach (var tag in tags)
                if (tag != null) result.Add(tag);
        }
        else if (warnedKeys.Add(key))
        {
            Debug.LogWarning($"[TagRules] Chưa có luật cho khóa '{key}'. " +
                             "Thêm một dòng vào TagRules (để trống nhãn nếu cố ý không có).");
        }
    }
}
