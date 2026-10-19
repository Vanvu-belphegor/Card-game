using System;
using System.Collections.Generic;
using System.Globalization;

// Kết quả tính một chỉ số, giữ lại từng thành phần để hiển thị công thức trong mô tả lá bài.
public readonly struct StatBreakdown
{
    public int BaseValue { get; }
    public double Flat { get; }          // tổng các modifier cộng thêm
    public double Multiplier { get; }    // tích các modifier nhân
    public int Result { get; }
    public bool IsModified => Flat != 0 || Multiplier != 1;

    public StatBreakdown(int baseValue, double flat, double multiplier, int result)
    {
        BaseValue = baseValue;
        Flat = flat;
        Multiplier = multiplier;
        Result = result;
    }

    // "5" khi chưa bị thay đổi; "5+5 = 10", "5*2 = 10" hoặc "(5+5)*5 = 50" khi có modifier
    public string ToFormulaString()
    {
        CultureInfo inv = CultureInfo.InvariantCulture;
        if (!IsModified) return BaseValue.ToString(inv);

        string expr = BaseValue.ToString(inv);
        if (Flat != 0)
        {
            expr += (Flat > 0 ? "+" : "-") + Math.Abs(Flat).ToString("0.##", inv);
            if (Multiplier != 1) expr = "(" + expr + ")";
        }
        if (Multiplier != 1) expr += "*" + Multiplier.ToString("0.##", inv);
        return expr + " = " + Result.ToString(inv);
    }
}

// Công thức: Result = (Base + tổng Flat) x tích các Multiplier, rồi làm tròn XUỐNG.
// Lớp này thuần C#: không biết gì về Card, Effect hay Unity.
public static class StatCalculator
{
    // Cộng thêm một chút trước khi làm tròn xuống để tránh sai số float
    // (ví dụ 10 x 0.3 có thể ra 2.9999999 thay vì 3).
    private const double Epsilon = 1e-4;

    public static int Calculate(int baseValue, StatKey key, IEnumerable<StatModifier> modifiers, int minValue = 0)
        => Breakdown(baseValue, key, modifiers, minValue).Result;

    public static StatBreakdown Breakdown(int baseValue, StatKey key, IEnumerable<StatModifier> modifiers, int minValue = 0)
    {
        if (modifiers == null) return new StatBreakdown(baseValue, 0, 1, baseValue);

        double flat = 0;
        double multiplier = 1;
        bool anyApplied = false;

        foreach (StatModifier m in modifiers)
        {
            if (m == null || !m.AppliesTo(key)) continue;
            anyApplied = true;
            if (m.Type == StatModifierType.Flat) flat += m.Value;
            else multiplier *= Math.Max(0f, m.Value);
        }

        // Không có modifier nào áp dụng: giữ nguyên giá trị gốc (hành vi như trước refactor)
        if (!anyApplied) return new StatBreakdown(baseValue, 0, 1, baseValue);

        double raw = (baseValue + flat) * multiplier;
        int result = Math.Max(minValue, (int)Math.Floor(raw + Epsilon));
        return new StatBreakdown(baseValue, flat, multiplier, result);
    }
}
