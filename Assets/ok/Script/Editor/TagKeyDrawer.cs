#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Vẽ ô Key của TagRules thành danh sách thả xuống:
//  - mỗi class effect (trừ abstract) là một mục, vd "DealDamageEffect"
//  - AddStatusEffectEffect được tách theo từng loại status: "Status/BURN" (giá trị lưu: "Status:BURN")
// Thêm class effect hoặc giá trị StatusEffectType mới thì danh sách tự có mục mới sau khi compile.
[CustomPropertyDrawer(typeof(TagKeyAttribute))]
public class TagKeyDrawer : PropertyDrawer
{
    private static string[] values;   // giá trị lưu vào asset
    private static string[] displays; // chữ hiện trong menu ("/" tạo menu con)

    private static void Build()
    {
        if (values != null) return;

        var v = new List<string>();
        var d = new List<string>();
        foreach (var type in TypeCache.GetTypesDerivedFrom<Effect>())
        {
            if (type.IsAbstract) continue;

            if (type == typeof(AddStatusEffectEffect))
            {
                foreach (var name in Enum.GetNames(typeof(StatusEffectType)))
                {
                    v.Add("Status:" + name);
                    d.Add("Status/" + name);
                }
            }
            else
            {
                v.Add(type.Name);
                d.Add(type.Name);
            }
        }

        // Sắp xếp theo giá trị, giữ cặp (giá trị, chữ hiện) đi cùng nhau
        var order = new List<int>();
        for (int i = 0; i < v.Count; i++) order.Add(i);
        order.Sort((a, b) => string.CompareOrdinal(v[a], v[b]));

        var sortedValues = new List<string> { "" };
        var sortedDisplays = new List<string> { "(chọn effect)" };
        foreach (int i in order)
        {
            sortedValues.Add(v[i]);
            sortedDisplays.Add(d[i]);
        }
        values = sortedValues.ToArray();
        displays = sortedDisplays.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Build();

        string current = property.stringValue ?? "";
        var shownValues = values;
        var shownDisplays = displays;

        // Khóa đã lưu nhưng không còn trong code (đổi tên/xóa class): hiện cảnh báo, không mất dữ liệu
        if (current != "" && Array.IndexOf(values, current) < 0)
        {
            var v = new List<string>(values);
            var d = new List<string>(displays);
            v.Insert(1, current);
            d.Insert(1, current + "  [không còn tồn tại]");
            shownValues = v.ToArray();
            shownDisplays = d.ToArray();
        }

        int index = Mathf.Max(0, Array.IndexOf(shownValues, current));

        EditorGUI.BeginProperty(position, label, property);
        int newIndex = EditorGUI.Popup(position, label.text, index, shownDisplays);
        if (newIndex != index) property.stringValue = shownValues[newIndex];
        EditorGUI.EndProperty();
    }
}
#endif
