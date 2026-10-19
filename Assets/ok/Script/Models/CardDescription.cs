using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

// Dựng mô tả lá bài: thay placeholder {damage}, {draw}, {burn}, {poison}, {armor},
// {strength}, {weak}, {vulnerable} bằng giá trị đã tính theo modifier + Strength của Hero.
// Chưa có modifier -> hiện số gốc "5". Có modifier -> hiện công thức vàng "(5+5)*5 = 50".
public static class CardDescription
{
    private static readonly Regex TokenRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);
    private const string Highlight = "#FFD54A";

    public static string Build(Card card, CombatantView hero)
    {
        string template = card.RawDescription ?? "";

        // Modifier của lá bài + modifier suy ra từ Strength của Hero (cùng nguồn với lúc gây sát thương)
        List<StatModifier> modifiers = new(card.Modifiers.Items);
        modifiers.AddRange(StatSources.FromCaster(hero));

        // token -> (chỉ số, giá trị gốc). Nếu có nhiều effect cùng loại thì lấy cái đầu tiên.
        Dictionary<string, (StatKey key, int baseValue)> stats = new();
        Collect(card.ManualTargetEffect, stats);
        if (card.OtherEffects != null)
            foreach (AutoTargetEffect wrapper in card.OtherEffects)
                if (wrapper != null) Collect(wrapper.Effect, stats);

        bool replacedAny = false;
        string result = TokenRegex.Replace(template, match =>
        {
            string token = match.Groups[1].Value.ToLowerInvariant();
            if (!stats.TryGetValue(token, out var s)) return match.Value; // token lạ: giữ nguyên

            replacedAny = true;
            StatBreakdown b = StatCalculator.Breakdown(s.baseValue, s.key, modifiers);
            string text = b.ToFormulaString();
            return b.IsModified ? $"<color={Highlight}>{text}</color>" : text;
        });

        // Lá cũ chưa dùng placeholder: giữ kiểu cũ "(x2)" để người chơi vẫn thấy lá đang được buff
        if (!replacedAny)
        {
            float m = card.Modifiers.GetUniversalMultiplier();
            if (m > 1f)
                result += $" <color={Highlight}>(x{m.ToString("0.##", CultureInfo.InvariantCulture)})</color>";
        }
        return result;
    }

    private static void Collect(Effect effect, Dictionary<string, (StatKey key, int baseValue)> stats)
    {
        if (effect == null || !effect.TryGetStat(out StatKey key, out int baseValue)) return;
        string token = key.Token;
        if (!stats.ContainsKey(token)) stats.Add(token, (key, baseValue));
    }
}
