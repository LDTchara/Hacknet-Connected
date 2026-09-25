using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerLoginCompEvent : PathfinderEvent
{
    public Computer Computer { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    public string Username { get; }
    public bool IsAdmin { get; }

    public PlayerLoginCompEvent(Computer computer, OS os, string username, bool isAdmin)
    {
        Computer = computer;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        Username = username;
        IsAdmin = isAdmin;
    }
}

internal static class LoginDetector
{
    private static string _lastComputerIP;
    private static string _lastUserName;
    private static bool _lastWasAdmin;

    [FrameDetect]
    internal static void Detect(OS os)
    {
        Computer comp = os.connectedComp;
        if (comp == null)
        {
            _lastComputerIP = null;
            _lastUserName = null;
            _lastWasAdmin = false;
            return;
        }

        string ip = comp.ip;
        bool isAdminNow = comp.adminIP == os.thisComputer.ip;
        string userName = comp.currentUser.name;

        if (ip != _lastComputerIP)
        {
            _lastUserName = null;
            _lastWasAdmin = false;
        }

        if (isAdminNow && !_lastWasAdmin)
        {
            EventManager<PlayerLoginCompEvent>.InvokeAll(
                new PlayerLoginCompEvent(comp, os, "admin", true)
            );
        }

        if (userName != null && userName != _lastUserName)
        {
            EventManager<PlayerLoginCompEvent>.InvokeAll(
                new PlayerLoginCompEvent(comp, os, userName, false)
            );
        }

        _lastComputerIP = ip;
        _lastUserName = userName;
        _lastWasAdmin = isAdminNow;
    }
}
