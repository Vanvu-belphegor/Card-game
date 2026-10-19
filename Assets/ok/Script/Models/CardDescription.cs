using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

// Dựng mô tả lá bài.
// - Chỉ hiện KẾT QUẢ cuối cùng (không còn công thức "(5+5)*5 = 50").
// - Mọi con số được in đậm. Số bị buff đổi sang màu vàng.
// - Không cần viết {damage}: cứ gõ "Gây 5 sát thương", số đầu tiên trùng với số gốc của effect
//   sẽ tự được thay bằng giá trị đã tính. Vẫn dùng được {damage}, {draw}, {burn}... nếu muốn.
// - Xuống dòng: gõ Enter trong ô Description (đã là TextArea) hoặc gõ \n.
public static class CardDescription
{
    private static readonly Regex TokenRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@"(?<![\w{])\d+(?![\w}])", RegexOptions.Compiled);
    // Bỏ qua thẻ <...> và {token}; chỉ bắt số nằm ngoài chúng
    private static readonly Regex BoldRegex = new(@"<[^>]*>|\{\w+\}|(\d+)", RegexOptions.Compiled);
    private const string Highlight = "#FFD54A";

    public static string Build(Card card, CombatantView hero)
    {
        string template = (card.RawDescription ?? "").Replace("\\n", "\n");

        // Modifier của lá bài + modifier suy ra từ Strength của Hero
        List<StatModifier> modifiers = new(card.Modifiers.Items);
        modifiers.AddRange(StatSources.FromCaster(hero));

        // token -> (chỉ số, giá trị gốc). Nhiều effect cùng loại thì lấy cái đầu tiên.
        Dictionary<string, (StatKey key, int baseValue)> stats = new();
        Collect(card.ManualTargetEffect, stats);
        if (card.OtherEffects != null)
            foreach (AutoTargetEffect wrapper in card.OtherEffects)
                if (wrapper != null) Collect(wrapper.Effect, stats);

        // 1) Tự gắn placeholder cho effect chưa có {token} trong mô tả
        template = AutoPlaceholders(template, stats);

        // 2) In đậm các số còn lại (số thường như "2 lần", "3 lá")
        template = BoldRegex.Replace(template, m => m.Groups[1].Success ? $"<b>{m.Value}</b>" : m.Value);

        // 3) Thay placeholder bằng kết quả cuối
        bool replacedAny = false;
        string result = TokenRegex.Replace(template, match =>
        {
            string token = match.Groups[1].Value.ToLowerInvariant();
            if (!stats.TryGetValue(token, out var s)) return match.Value; // token lạ: giữ nguyên

            replacedAny = true;
            StatBreakdown b = StatCalculator.Breakdown(s.baseValue, s.key, modifiers);
            string text = b.Result.ToString(CultureInfo.InvariantCulture);
            return b.IsModified ? $"<b><color={Highlight}>{text}</color></b>" : $"<b>{text}</b>";
        });

        // Lá chưa có số nào để thay: hiện "(x2)" để người chơi vẫn thấy lá đang được buff
        if (!replacedAny)
        {
            float m = card.Modifiers.GetUniversalMultiplier();
            if (m > 1f)
                result += $" <b><color={Highlight}>(x{m.ToString("0.##", CultureInfo.InvariantCulture)})</color></b>";
        }
        return result;
    }

    // Với mỗi effect chưa có {token}: đổi số đầu tiên (chưa dùng) bằng đúng giá trị gốc thành {token}
    private static string AutoPlaceholders(string template, Dictionary<string, (StatKey key, int baseValue)> stats)
    {
        HashSet<int> usedPositions = new();
        foreach (var kv in stats)
        {
            if (template.IndexOf("{" + kv.Key + "}", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            foreach (Match m in NumberRegex.Matches(template))
            {
                if (usedPositions.Contains(m.Index)) continue;
                if (m.Value != kv.Value.baseValue.ToString(CultureInfo.InvariantCulture)) continue;

                string placeholder = "{" + kv.Key + "}";
                template = template.Substring(0, m.Index) + placeholder + template.Substring(m.Index + m.Length);
                // Vị trí sau đó đã dịch chuyển -> tính lại từ đầu cho effect kế tiếp
                usedPositions.Clear();
                break;
            }
        }
        return template;
    }

    private static void Collect(Effect effect, Dictionary<string, (StatKey key, int baseValue)> stats)
    {
        if (effect == null || !effect.TryGetStat(out StatKey key, out int baseValue)) return;
        string token = key.Token;
        if (!stats.ContainsKey(token)) stats.Add(token, (key, baseValue));
    }
}
