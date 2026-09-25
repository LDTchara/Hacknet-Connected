using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

public enum FactionAction
{
    Joined,      // currentFaction: null → 非null
    Left,        // currentFaction: 非null → null
    Changed,     // currentFaction: 切换
    ValueAdded,  // playerValue 增加
    RankUp       // playerHasPassedValue: false → true
}

[EventAutoSubscribe]
public class PlayerFactionEvent : PathfinderEvent
{
    public Faction Faction { get; }
    public FactionAction Action { get; }
    public string CurrentPlayerIP { get; }
    public string FactionName { get; }
    public string FactionID { get; }
    public int PlayerValue { get; }
    public int NeededValue { get; }
    public int OldValue { get; }     // 变更前的值（仅 ValueAdded 有效）
    public bool IsRankUp { get; }    // 是否刚升级
    public OS Os { get; }

    public PlayerFactionEvent(Faction faction, OS os, FactionAction action,
        int oldValue = -1, bool isRankUp = false)
    {
        Faction = faction;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        Action = action;
        OldValue = oldValue;
        IsRankUp = isRankUp;

        if (faction != null)
        {
            FactionName = faction.name;
            FactionID = faction.idName;
            PlayerValue = faction.playerValue;
            NeededValue = faction.neededValue;
        }
    }
}

internal static class FactionDetector
{
    private static Faction _lastFaction;
    private static int _lastPlayerValue;
    private static bool _lastHasPassed;

    [FrameDetect]
    internal static void Detect(OS os)
    {
        // ── 切换检测 ──
        if (os.currentFaction != _lastFaction)
        {
            if (os.currentFaction != null && _lastFaction == null)
                Emit(os.currentFaction, os, FactionAction.Joined);
            else if (os.currentFaction == null && _lastFaction != null)
                Emit(_lastFaction, os, FactionAction.Left);
            else
                Emit(os.currentFaction, os, FactionAction.Changed);

            _lastFaction = os.currentFaction;
            _lastPlayerValue = os.currentFaction?.playerValue ?? 0;
            _lastHasPassed = os.currentFaction?.playerHasPassedValue ?? false;
            return;
        }

        var f = os.currentFaction;
        if (f == null) return;

        // ── Value 变化检测 ──
        if (f.playerValue != _lastPlayerValue)
        {
            bool rankUp = f.playerHasPassedValue && !_lastHasPassed;
            Emit(f, os, FactionAction.ValueAdded, _lastPlayerValue, rankUp);
            _lastPlayerValue = f.playerValue;
        }

        // ── RankUp 单独检测（非 value 变化导致的升级） ──
        if (f.playerHasPassedValue != _lastHasPassed)
        {
            if (f.playerHasPassedValue)
                Emit(f, os, FactionAction.RankUp);
            _lastHasPassed = f.playerHasPassedValue;
        }
    }

    static void Emit(Faction f, OS os, FactionAction a, int oldVal = -1, bool rankUp = false)
    {
        EventManager<PlayerFactionEvent>.InvokeAll(
            new PlayerFactionEvent(f, os, a, oldVal, rankUp));
    }
}
