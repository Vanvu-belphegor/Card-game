#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Tools > Card Tags > Find Cards
// Chọn loại và nhãn (giống CardFilter) rồi xem lá nào trong project khớp.
// Dùng đúng CardFilter.Matches của game nên kết quả giống hệt khi chạy.
public class CardTagFinderWindow : EditorWindow
{
    [SerializeField] private CardType type = CardType.None;
    [SerializeField] private List<CardTag> anyOf = new();
    [SerializeField] private List<CardTag> allOf = new();
    [SerializeField] private List<CardTag> noneOf = new();

    private List<CardData> allCards = new();
    private Vector2 scroll;

    [MenuItem("Tools/Card Tags/Find Cards")]
    public static void Open() => GetWindow<CardTagFinderWindow>("Find Cards");

    private void OnEnable() => Refresh();

    private void Refresh()
    {
        allCards = AssetDatabase.FindAssets("t:CardData")
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(card => card != null)
            .OrderBy(card => card.name)
            .ToList();
    }

    private void OnGUI()
    {
        var so = new SerializedObject(this);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("type"));
        EditorGUILayout.PropertyField(so.FindProperty("anyOf"), new GUIContent("Any Of (có ít nhất 1)"), true);
        EditorGUILayout.PropertyField(so.FindProperty("allOf"), new GUIContent("All Of (có tất cả)"), true);
        EditorGUILayout.PropertyField(so.FindProperty("noneOf"), new GUIContent("None Of (không có)"), true);
        so.ApplyModifiedProperties();

        if (GUILayout.Button("Làm mới danh sách lá")) Refresh();

        EditorGUILayout.Space(4);

        var filter = new CardFilter(type, anyOf, allOf, noneOf);
        int matches = 0;

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var data in allCards)
        {
            var card = new Card(data);
            if (!filter.Matches(card)) continue;
            matches++;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(data, typeof(CardData), false, GUILayout.Width(160));
                EditorGUILayout.LabelField(CardDataEditor.Join(card.Tags), EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Mở", GUILayout.Width(44))) Selection.activeObject = data;
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField($"Khớp {matches} / {allCards.Count} lá (bộ lọc trống = tất cả)", EditorStyles.miniLabel);
    }
}
#endif
