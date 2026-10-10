// Assets/Editor/ActionSystemEditor.cs
// Bản rút gọn: bỏ Replay, bấm vào tên hàm để mở thẳng code, log dạng cây cha/con thu gọn được.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionSystem))]
public class ActionSystemEditor : Editor
{
    // ── Foldouts ─────────────────────────────────────────────────────
    private bool showRuntime = true;
    private bool showPerformers = true;
    private bool showPreSubs = true;
    private bool showPostSubs = true;
    private bool showLogs = true;

    private Dictionary<string, bool> preSubFolds = new();
    private Dictionary<string, bool> postSubFolds = new();
    private Dictionary<string, bool> logFolds = new();   // mỗi log gốc
    private Dictionary<string, bool> nodeFolds = new();  // mỗi GA con

    // ── Log state ────────────────────────────────────────────────────
    private Vector2 logScroll = Vector2.zero;
    private int maxLogsField = 50;

    private bool filterPRE = true;
    private bool filterPERFORM = true;
    private bool filterPOST = true;
    private bool filterREACTION = true;

    // ── Colors ───────────────────────────────────────────────────────
    private static readonly Color ColGreen = new Color(0.2f, 0.8f, 0.3f, 0.22f);
    private static readonly Color ColBlue = new Color(0.3f, 0.6f, 1f, 0.18f);
    private static readonly Color ColOrange = new Color(1f, 0.6f, 0.2f, 0.18f);
    private static readonly Color ColYellow = new Color(1f, 0.9f, 0.1f, 0.18f);

    private static Color TagPRE => new Color(0.35f, 0.75f, 1f);
    private static Color TagPERFORM => new Color(1f, 0.85f, 0.2f);
    private static Color TagPOST => new Color(1f, 0.55f, 0.15f);
    private static Color TagREACTION => new Color(0.75f, 0.45f, 1f);
    private static Color TagHEADER => new Color(0.9f, 0.9f, 0.9f);

    // Mỗi cấp GA một màu nền khác nhau để thấy rõ cha/con
    private static readonly Color[] LevelColors =
    {
        new Color(0.3f, 0.6f, 1f, 0.10f),
        new Color(0.7f, 0.4f, 1f, 0.12f),
        new Color(0.2f, 0.8f, 0.5f, 0.12f),
        new Color(1f, 0.6f, 0.2f, 0.12f),
        new Color(1f, 0.4f, 0.5f, 0.12f),
    };
    private static Color LevelColor(int level) => LevelColors[level % LevelColors.Length];

    // ─────────────────────────────────────────────────────────────────
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("═══ Action System Debugger ═══", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawRuntime();
        EditorGUILayout.Space(4);
        DrawPerformers();
        EditorGUILayout.Space(4);
        DrawSubs("PRE", ref showPreSubs, preSubFolds, ColBlue);
        EditorGUILayout.Space(4);
        DrawSubs("POST", ref showPostSubs, postSubFolds, ColOrange);
        EditorGUILayout.Space(4);
        DrawLogSection();

        if (Application.isPlaying) Repaint();
    }

    // ─────────────────────────────────────────────────────────────────
    // RUNTIME
    // ─────────────────────────────────────────────────────────────────
    void DrawRuntime()
    {
        showRuntime = EditorGUILayout.Foldout(showRuntime, "Runtime State", true, EditorStyles.foldoutHeader);
        if (!showRuntime) return;
        EditorGUI.indentLevel++;
        if (!Application.isPlaying)
        { EditorGUILayout.HelpBox("Vào Play Mode để xem trạng thái runtime.", MessageType.Info); EditorGUI.indentLevel--; return; }

        ActionSystem sys = (ActionSystem)target;
        bool p = sys.IsPerforming;
        string ph = sys.CurrentPhase;
        string an = sys.CurrentAction?.GetType().Name ?? "—";

        ColorBox(p ? PhaseColor(ph) : Color.clear, () =>
        {
            GUIStyle vs = new GUIStyle(EditorStyles.boldLabel);
            vs.normal.textColor = p ? new Color(0.2f, 0.95f, 0.3f) : Color.white;
            EditorGUILayout.LabelField("Is Performing", p ? "✔ TRUE" : "✖ FALSE", vs);
            if (p)
            {
                EditorGUILayout.LabelField("Current Action", an);
                EditorGUILayout.LabelField("Current Phase",
                    ph == "PRE" ? "◀ PRE – subscribers đang được notify" :
                    ph == "PERFORM" ? "▶ PERFORM – performer đang chạy" :
                    ph == "POST" ? "▶ POST – subscribers đang được notify" : ph);
                if (sys.CurrentAction != null)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Reaction Queues:", EditorStyles.miniBoldLabel);
                    EditorGUI.indentLevel++;
                    DrawReactionList("PreReactions", sys.CurrentAction);
                    DrawReactionList("PerformReactions", sys.CurrentAction);
                    DrawReactionList("PostReactions", sys.CurrentAction);
                    EditorGUI.indentLevel--;
                }
            }
        });
        EditorGUI.indentLevel--;
    }

    void DrawReactionList(string propName, GameAction action)
    {
        var pi = typeof(GameAction).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
        if (pi == null) return;
        IList list = pi.GetValue(action) as IList;
        int cnt = list?.Count ?? 0;
        EditorGUILayout.LabelField($"{propName}: {(cnt == 0 ? "(trống)" : cnt + " reaction")}");
        if (cnt > 0)
        { EditorGUI.indentLevel++; foreach (var it in list) EditorGUILayout.LabelField("• " + it?.GetType().Name); EditorGUI.indentLevel--; }
    }

    // ─────────────────────────────────────────────────────────────────
    // PERFORMERS  (bấm vào tên hàm để mở code)
    // ─────────────────────────────────────────────────────────────────
    void DrawPerformers()
    {
        IDictionary pf = ReadStaticDict("performers");
        IDictionary info = ReadStaticDict("performerInfo");
        int cnt = pf?.Count ?? 0;
        showPerformers = EditorGUILayout.Foldout(showPerformers, $"Performers [{cnt}]", true, EditorStyles.foldoutHeader);
        if (!showPerformers) return;
        EditorGUI.indentLevel++;
        if (cnt == 0) { EditorGUILayout.LabelField("(chưa có performer nào)"); EditorGUI.indentLevel--; return; }

        string running = "";
        if (Application.isPlaying)
        {
            ActionSystem sys = (ActionSystem)target;
            if (sys.IsPerforming && sys.CurrentPhase == "PERFORM" && sys.CurrentAction != null)
                running = sys.CurrentAction.GetType().Name;
        }

        foreach (DictionaryEntry e in pf)
        {
            Type t = (Type)e.Key;
            string name = t.Name;
            bool isR = name == running;
            object tuple = (info != null && info.Contains(t)) ? info[t] : null;

            ColorBox(isR ? ColGreen : Color.clear, () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((isR ? "▶ " : "• ") + name + (isR ? "  ← RUNNING" : ""),
                    isR ? EditorStyles.boldLabel : EditorStyles.label);
                GUILayout.FlexibleSpace();
                CodeButtons(name, tuple);
                EditorGUILayout.EndHorizontal();
            });
        }
        EditorGUI.indentLevel--;
    }

    // ─────────────────────────────────────────────────────────────────
    // SUBSCRIBERS  (bấm vào tên hàm để mở code)
    // ─────────────────────────────────────────────────────────────────
    void DrawSubs(string timing, ref bool foldout, Dictionary<string, bool> folds, Color hdrColor)
    {
        IDictionary sub = ReadStaticDict(timing == "PRE" ? "preSubs" : "postSubs");
        IDictionary info = ReadStaticDict(timing == "PRE" ? "preSubInfo" : "postSubInfo");
        int cnt = sub?.Count ?? 0;
        foldout = EditorGUILayout.Foldout(foldout, $"{timing} Reaction Subscribers [{cnt} action type]", true, EditorStyles.foldoutHeader);
        if (!foldout) return;
        EditorGUI.indentLevel++;
        if (cnt == 0) { EditorGUILayout.LabelField("(chưa có subscriber nào)"); EditorGUI.indentLevel--; return; }

        foreach (DictionaryEntry e in sub)
        {
            Type t = (Type)e.Key; string name = t.Name;
            IList list = e.Value as IList;
            IList inf = (info != null && info.Contains(t)) ? info[t] as IList : null;
            if (!folds.ContainsKey(name)) folds[name] = false;
            ColorBox(hdrColor, () =>
            {
                EditorGUILayout.BeginHorizontal();
                folds[name] = EditorGUILayout.Foldout(folds[name], $"• {name} ({list?.Count ?? 0} subscriber)", true);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("GA", EditorStyles.miniButton, GUILayout.Width(30))) OpenCode(name, null);
                EditorGUILayout.EndHorizontal();

                if (folds[name])
                {
                    if (inf != null)
                        for (int i = 0; i < inf.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(24);
                            EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(28));
                            CodeButtons(null, inf[i]);
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                        }
                    else EditorGUILayout.LabelField("(không có info)");
                }
            });
        }
        EditorGUI.indentLevel--;
    }

    // ─────────────────────────────────────────────────────────────────
    // LOG SECTION  (cây cha/con, thu gọn được)
    // ─────────────────────────────────────────────────────────────────
    void DrawLogSection()
    {
        var logs = ActionSystem.ActionLogs;
        showLogs = EditorGUILayout.Foldout(showLogs, $"Action Logs [{logs.Count}]", true, EditorStyles.foldoutHeader);
        if (!showLogs) return;
        EditorGUI.indentLevel++;

        // Toolbar
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Max:", GUILayout.Width(30));
        maxLogsField = EditorGUILayout.IntField(maxLogsField, GUILayout.Width(40));
        if (maxLogsField > 0 && maxLogsField != ActionSystem.MaxLogs) ActionSystem.MaxLogs = maxLogsField;
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Mở hết", EditorStyles.miniButton, GUILayout.Width(60))) SetAllFolds(true);
        if (GUILayout.Button("Thu hết", EditorStyles.miniButton, GUILayout.Width(60))) SetAllFolds(false);
        if (GUILayout.Button("Clear All", GUILayout.Width(70))) { ActionSystem.ActionLogs.Clear(); logFolds.Clear(); nodeFolds.Clear(); }
        EditorGUILayout.EndHorizontal();

        // Filter
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Filter:", GUILayout.Width(42));
        FilterToggle("PRE", ref filterPRE, TagPRE);
        FilterToggle("PERFORM", ref filterPERFORM, TagPERFORM);
        FilterToggle("POST", ref filterPOST, TagPOST);
        FilterToggle("REACTION", ref filterREACTION, TagREACTION);
        EditorGUILayout.EndHorizontal();

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Log được ghi trong Play Mode.", MessageType.Info);

        if (logs.Count == 0) { EditorGUILayout.LabelField("(chưa có log nào)"); EditorGUI.indentLevel--; return; }

        EditorGUILayout.Space(2);
        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.MaxHeight(520));
        for (int i = logs.Count - 1; i >= 0; i--)
            DrawLog(i, logs[i]);
        EditorGUILayout.EndScrollView();

        EditorGUI.indentLevel--;
    }

    // Một log gốc (GA cha ngoài cùng)
    void DrawLog(int index, ActionSystem.ActionLog log)
    {
        string lk = LogKey(log);
        if (!logFolds.TryGetValue(lk, out bool open)) open = false;

        string status = log.isFinished ? "✔" : "⏳";
        string dur = log.isFinished ? $"{log.duration * 1000f:F1} ms" : "running...";
        int childCount = CountHeaders(log) - 1;

        ColorBox(LevelColor(0), () =>
        {
            EditorGUILayout.BeginHorizontal();
            GUIStyle fs = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            open = EditorGUILayout.Foldout(open, $"{status} [{index}] GỐC: {log.actionName} — {dur}  ({childCount} GA con)", true, fs);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("GA", EditorStyles.miniButton, GUILayout.Width(30))) OpenCode(log.actionName, null);
            EditorGUILayout.EndHorizontal();
            logFolds[lk] = open;

            if (open)
            {
                Node root = BuildTree(log.entries);
                if (root != null) DrawNodeBody(root, lk, 1, root.header.text);
            }
        });
    }

    // Thân của một GA: các dòng PRE/PERFORM/POST và các GA con xen kẽ theo đúng thứ tự
    void DrawNodeBody(Node n, string lk, int level, string parentName)
    {
        foreach (object item in n.items)
        {
            if (item is ActionSystem.ActionLogEntry e) DrawLine(e, level);
            else if (item is Node child) DrawChild(child, lk, level, parentName);
        }
    }

    // Một GA con, có thể thu gọn
    void DrawChild(Node child, string lk, int level, string parentName)
    {
        string key = lk + "_" + child.idx;
        if (!nodeFolds.TryGetValue(key, out bool open)) open = true;

        string dur = child.footer != null
            ? $"{child.footer.elapsed - child.header.elapsed:F1} ms"
            : "running...";
        string kids = child.childCount > 0 ? $"  ({child.childCount} GA con)" : "";

        ColorBox(LevelColor(level), () =>
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(level * 8f);
            GUIStyle fs = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            open = EditorGUILayout.Foldout(open,
                $"↳ CON: {child.header.text}   ← con của {parentName}   {dur}{kids}", true, fs);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("GA", EditorStyles.miniButton, GUILayout.Width(30))) OpenCode(child.header.text, null);
            EditorGUILayout.EndHorizontal();
            nodeFolds[key] = open;

            if (open) DrawNodeBody(child, lk, level + 1, child.header.text);
        });
    }

    // Một dòng PRE / PERFORM / POST / REACTION
    void DrawLine(ActionSystem.ActionLogEntry e, int level)
    {
        if (e.kind == ActionSystem.LogEntryKind.PRE && !filterPRE) return;
        if (e.kind == ActionSystem.LogEntryKind.PERFORM && !filterPERFORM) return;
        if (e.kind == ActionSystem.LogEntryKind.POST && !filterPOST) return;
        if (e.kind == ActionSystem.LogEntryKind.REACTION && !filterREACTION) return;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(level * 8f + 18f);

        GUIStyle ts = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
        ts.normal.textColor = EntryTagColor(e.kind);
        EditorGUILayout.LabelField(KindLabel(e.kind), ts, GUILayout.Width(70));

        GUIStyle ms = new GUIStyle(EditorStyles.miniLabel);
        ms.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        EditorGUILayout.LabelField($"+{e.elapsed:F1}ms", ms, GUILayout.Width(60));

        GUIStyle ds = new GUIStyle(EditorStyles.miniLabel);
        ds.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        EditorGUILayout.LabelField(e.text, ds);

        // Dòng "Notify → Class.Method" hoặc "Run → Class.Method": bấm để mở code
        var m = Regex.Match(e.text ?? "", @"(?:→|✔)\s+(\w+)\.(\w+)");
        if (m.Success && m.Groups[1].Value != "static")
        {
            if (GUILayout.Button("mở", EditorStyles.miniButton, GUILayout.Width(36)))
                OpenCode(m.Groups[1].Value, m.Groups[2].Value);
        }
        EditorGUILayout.EndHorizontal();
    }

    // ── Dựng cây từ danh sách entry phẳng (Header = mở GA, Footer = đóng GA) ──
    class Node
    {
        public int idx;
        public ActionSystem.ActionLogEntry header;
        public ActionSystem.ActionLogEntry footer;
        public List<object> items = new();
        public int childCount;
    }

    Node BuildTree(List<ActionSystem.ActionLogEntry> entries)
    {
        Node root = null;
        var stack = new Stack<Node>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.kind == ActionSystem.LogEntryKind.Header)
            {
                var n = new Node { idx = i, header = e };
                if (stack.Count > 0) { stack.Peek().items.Add(n); stack.Peek().childCount++; }
                else if (root == null) root = n;
                stack.Push(n);
            }
            else if (e.kind == ActionSystem.LogEntryKind.Footer)
            {
                if (stack.Count > 0) stack.Pop().footer = e;
            }
            else if (stack.Count > 0)
            {
                stack.Peek().items.Add(e);
            }
        }
        return root;
    }

    int CountHeaders(ActionSystem.ActionLog log)
    {
        int c = 0;
        foreach (var e in log.entries) if (e.kind == ActionSystem.LogEntryKind.Header) c++;
        return c;
    }

    string LogKey(ActionSystem.ActionLog log) => "L" + log.startTime.ToString("R");

    void SetAllFolds(bool value)
    {
        foreach (var log in ActionSystem.ActionLogs)
        {
            string lk = LogKey(log);
            logFolds[lk] = value;
            Node root = BuildTree(log.entries);
            if (root != null) SetNodeFolds(root, lk, value);
        }
    }

    void SetNodeFolds(Node n, string lk, bool value)
    {
        foreach (object it in n.items)
            if (it is Node c) { nodeFolds[lk + "_" + c.idx] = value; SetNodeFolds(c, lk, value); }
    }

    // ─────────────────────────────────────────────────────────────────
    // MỞ CODE
    // ─────────────────────────────────────────────────────────────────

    // Vẽ nút "Class.Method ↗". tuple = (object target, string method) từ ActionSystem
    void CodeButtons(string gaName, object tuple)
    {
        if (gaName != null)
            if (GUILayout.Button("GA", EditorStyles.miniButton, GUILayout.Width(30))) OpenCode(gaName, null);

        if (!TryInfo(tuple, out string cls, out string method))
        {
            EditorGUILayout.LabelField("?", GUILayout.Width(20));
            return;
        }

        if (cls == null) // hàm static: không biết class
        {
            EditorGUILayout.LabelField("static." + method, EditorStyles.miniLabel, GUILayout.Width(180));
            return;
        }

        if (GUILayout.Button($"{cls}.{method}  ↗", EditorStyles.miniButton, GUILayout.MinWidth(200)))
            OpenCode(cls, method);
    }

    bool TryInfo(object tuple, out string cls, out string method)
    {
        cls = null; method = null;
        if (tuple == null) return false;
        Type t = tuple.GetType();
        var f1 = t.GetField("Item1"); var f2 = t.GetField("Item2");
        if (f1 == null || f2 == null) return false;
        cls = f1.GetValue(tuple)?.GetType().Name; // null nếu static
        method = f2.GetValue(tuple) as string ?? "?";
        return true;
    }

    // Mở script của class, nhảy tới dòng khai báo hàm (method == null -> dòng khai báo class)
    static void OpenCode(string className, string method)
    {
        string path = FindScriptPath(className);
        if (path == null)
        {
            Debug.LogWarning($"[ActionSystemEditor] Không tìm thấy script của class '{className}'.");
            return;
        }
        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        int line = 1;
        try
        {
            string[] lines = File.ReadAllLines(path);
            Regex rx = method != null
                ? new Regex(@"(?<![\.\w])" + Regex.Escape(method) + @"\s*\(")
                : new Regex(@"\bclass\s+" + Regex.Escape(className) + @"\b");
            for (int i = 0; i < lines.Length; i++)
            {
                if (!rx.IsMatch(lines[i])) continue;
                if (method != null && lines[i].TrimEnd().EndsWith(";")) continue; // bỏ lời gọi hàm
                line = i + 1;
                break;
            }
        }
        catch { }
        AssetDatabase.OpenAsset(script, line);
    }

    static string FindScriptPath(string className)
    {
        string[] guids = AssetDatabase.FindAssets(className + " t:MonoScript");
        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == className) return p;
        }
        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
            if (ms != null && ms.GetClass() != null && ms.GetClass().Name == className) return p;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────
    Color EntryTagColor(ActionSystem.LogEntryKind kind) => kind switch
    {
        ActionSystem.LogEntryKind.PRE => TagPRE,
        ActionSystem.LogEntryKind.PERFORM => TagPERFORM,
        ActionSystem.LogEntryKind.POST => TagPOST,
        ActionSystem.LogEntryKind.REACTION => TagREACTION,
        _ => TagHEADER,
    };

    string KindLabel(ActionSystem.LogEntryKind kind) => kind switch
    {
        ActionSystem.LogEntryKind.PRE => "[PRE]",
        ActionSystem.LogEntryKind.PERFORM => "[PERFORM]",
        ActionSystem.LogEntryKind.POST => "[POST]",
        ActionSystem.LogEntryKind.REACTION => "[REACT]",
        ActionSystem.LogEntryKind.Header => "[BEGIN]",
        ActionSystem.LogEntryKind.Footer => "[END]",
        _ => "[?]",
    };

    void FilterToggle(string label, ref bool val, Color col)
    {
        GUIStyle s = new GUIStyle(EditorStyles.miniButton);
        s.normal.textColor = val ? col : new Color(0.45f, 0.45f, 0.45f);
        s.fontStyle = val ? FontStyle.Bold : FontStyle.Normal;
        if (GUILayout.Button(label, s, GUILayout.Width(62))) val = !val;
    }

    IDictionary ReadStaticDict(string fieldName)
    {
        FieldInfo fi = typeof(ActionSystem).GetField(fieldName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return fi?.GetValue(null) as IDictionary;
    }

    Color PhaseColor(string phase) => phase switch
    {
        "PRE" => ColBlue,
        "POST" => ColOrange,
        "PERFORM" => ColYellow,
        _ => ColGreen
    };

    void ColorBox(Color color, Action draw)
    {
        if (color.a > 0.01f)
        { Rect r = EditorGUILayout.BeginVertical(); EditorGUI.DrawRect(r, color); draw(); EditorGUILayout.EndVertical(); }
        else
        { EditorGUILayout.BeginVertical(); draw(); EditorGUILayout.EndVertical(); }
    }
}
