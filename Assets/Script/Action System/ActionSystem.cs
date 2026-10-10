using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionSystem : Singleton<ActionSystem>
{
    private List<GameAction> reactions = null;
    public bool IsPerforming { get; private set; } = false;

    public GameAction CurrentAction { get; private set; } = null;
    public string CurrentPhase { get; private set; } = "";

    public static Dictionary<Type, List<Action<GameAction>>> preSubs = new();
    public static Dictionary<Type, List<Action<GameAction>>> postSubs = new();
    public static Dictionary<Type, Func<GameAction, IEnumerator>> performers = new();

    public static Dictionary<Type, (object target, string method)> performerInfo = new();
    public static Dictionary<Type, List<(object target, string method)>> preSubInfo = new();
    public static Dictionary<Type, List<(object target, string method)>> postSubInfo = new();

    private static Dictionary<(Delegate, ReactionTiming), Action<GameAction>> wrappedDelegates = new();

    // ── Action Log ───────────────────────────────────────────────────
    public static List<ActionLog> ActionLogs = new();
    public static int MaxLogs = 50;

    public enum LogEntryKind { Header, PRE, PERFORM, POST, REACTION, Footer }

    public class ActionLogEntry
    {
        public LogEntryKind kind;
        public string text;
        public float elapsed;   // ms kể từ startTime của root log
        public int depth;     // độ sâu lồng nhau (0 = root)
    }

    public class ActionLog
    {
        public string actionName;
        public float startTime;
        public float endTime;
        public float duration => endTime - startTime;
        public bool isFinished;
        public List<ActionLogEntry> entries = new();

        public void Add(LogEntryKind kind, string text, float startTime, int depth)
        {
            entries.Add(new ActionLogEntry
            {
                kind = kind,
                text = text,
                elapsed = (Time.time - startTime) * 1000f,
                depth = depth,
            });
        }
    }
    // ────────────────────────────────────────────────────────────────

    public void Perform(GameAction action, System.Action OnPerformFinished = null)
    {
        if (IsPerforming) return;
        IsPerforming = true;
        StartCoroutine(Flow(action, () =>
        {
            IsPerforming = false;
            CurrentAction = null;
            CurrentPhase = "";
            OnPerformFinished?.Invoke();
        }));
    }

    public void AddReaction(GameAction gameAction) => reactions?.Add(gameAction);

    private IEnumerator Flow(GameAction action, Action OnFlowFinished = null,
                             ActionLog parentLog = null, int depth = 0)
    {
        CurrentAction = action;
        string name = action.GetType().Name;

        bool isRoot = parentLog == null;
        ActionLog log = isRoot ? null : parentLog;
        float t0 = isRoot ? Time.time : parentLog.startTime;

        if (isRoot)
        {
            log = new ActionLog { actionName = name, startTime = Time.time };
            if (ActionLogs.Count >= MaxLogs) ActionLogs.RemoveAt(0);
            ActionLogs.Add(log);
        }

        // ── Header action ────────────────────────────────────────────
        log.Add(LogEntryKind.Header, name, t0, depth);

        // PRE
        CurrentPhase = "PRE";
        reactions = action.PreReactions;
        LogSubscribers(log, action, preSubs, preSubInfo, LogEntryKind.PRE, t0, depth);
        yield return PerformReactions(log, t0, depth + 1);

        // PERFORM
        CurrentPhase = "PERFORM";
        reactions = action.PerformReactions;
        yield return PerformPerformerLogged(action, log, t0, depth);
        yield return PerformReactions(log, t0, depth + 1);

        // POST
        CurrentPhase = "POST";
        reactions = action.PostReactions;
        LogSubscribers(log, action, postSubs, postSubInfo, LogEntryKind.POST, t0, depth);
        yield return PerformReactions(log, t0, depth + 1);

        // ── Footer action ────────────────────────────────────────────
        log.Add(LogEntryKind.Footer, name, t0, depth);

        if (isRoot) { log.endTime = Time.time; log.isFinished = true; }
        OnFlowFinished?.Invoke();
    }

    private void LogSubscribers(
        ActionLog log, GameAction action,
        Dictionary<Type, List<Action<GameAction>>> subs,
        Dictionary<Type, List<(object target, string method)>> infoMap,
        LogEntryKind kind, float t0, int depth)
    {
        Type type = action.GetType();
        if (!subs.TryGetValue(type, out var list)) return;

        infoMap.TryGetValue(type, out var infos);
        var snapshot = new List<Action<GameAction>>(list);

        for (int i = 0; i < snapshot.Count; i++)
        {
            string who = (infos != null && i < infos.Count)
                ? $"{infos[i].target?.GetType().Name ?? "static"}.{infos[i].method}"
                : $"subscriber[{i}]";
            log.Add(kind, $"Notify → {who}", t0, depth);
            snapshot[i](action);
            if (reactions != null)
                foreach (var r in reactions)
                    log.Add(LogEntryKind.REACTION, $"AddReaction: {r.GetType().Name}", t0, depth);
        }
    }

    private IEnumerator PerformPerformerLogged(GameAction action, ActionLog log, float t0, int depth)
    {
        Type type = action.GetType();
        if (!performers.TryGetValue(type, out var performer))
        {
            log.Add(LogEntryKind.PERFORM, "(no performer)", t0, depth);
            yield break;
        }
        string who = performerInfo.TryGetValue(type, out var info)
            ? $"{info.target?.GetType().Name ?? "static"}.{info.method}"
            : "?";
        log.Add(LogEntryKind.PERFORM, $"Run → {who}", t0, depth);
        yield return performer(action);
        log.Add(LogEntryKind.PERFORM, $"Done ✔ {who}", t0, depth);
    }

    private IEnumerator PerformReactions(ActionLog log, float t0, int depth)
    {
        if (reactions == null) yield break;
        foreach (var reaction in reactions)
            yield return Flow(reaction, null, log, depth);
    }

    // ── Registration ─────────────────────────────────────────────────

    public static void AttachPerformer<T>(Func<T, IEnumerator> performer) where T : GameAction
    {
        Type type = typeof(T);
        IEnumerator wrapped(GameAction a) => performer((T)a);
        performers[type] = wrapped;
        performerInfo[type] = (performer.Target, performer.Method.Name);
    }

    public static void DetachPerformer<T>() where T : GameAction
    {
        performers.Remove(typeof(T));
        performerInfo.Remove(typeof(T));
    }

    public static void SubscribeReaction<T>(Action<T> reaction, ReactionTiming timing) where T : GameAction
    {
        var subs = timing == ReactionTiming.PRE ? preSubs : postSubs;
        var infoMap = timing == ReactionTiming.PRE ? preSubInfo : postSubInfo;
        var key = (reaction as Delegate, timing);
        if (wrappedDelegates.ContainsKey(key)) return;

        Action<GameAction> wrapped = a => reaction((T)a);
        wrappedDelegates[key] = wrapped;

        if (!subs.TryGetValue(typeof(T), out var list))
        { list = new List<Action<GameAction>>(); subs[typeof(T)] = list; }
        list.Add(wrapped);

        if (!infoMap.TryGetValue(typeof(T), out var infos))
        { infos = new List<(object, string)>(); infoMap[typeof(T)] = infos; }
        infos.Add((reaction.Target, reaction.Method.Name));
    }

    public static void UnsubscribeReaction<T>(Action<T> reaction, ReactionTiming timing) where T : GameAction
    {
        var subs = timing == ReactionTiming.PRE ? preSubs : postSubs;
        var infoMap = timing == ReactionTiming.PRE ? preSubInfo : postSubInfo;
        var key = (reaction as Delegate, timing);

        if (!wrappedDelegates.TryGetValue(key, out var wrapped)) return;
        if (subs.TryGetValue(typeof(T), out var list))
        {
            int idx = list.IndexOf(wrapped);
            list.Remove(wrapped);
            if (infoMap.TryGetValue(typeof(T), out var infos) && idx >= 0 && idx < infos.Count)
                infos.RemoveAt(idx);
        }
        wrappedDelegates.Remove(key);
    }

    public static void ResetStaticState()
    {
        preSubs.Clear(); postSubs.Clear();
        performers.Clear(); performerInfo.Clear();
        preSubInfo.Clear(); postSubInfo.Clear();
        wrappedDelegates.Clear(); ActionLogs.Clear();
    }
}