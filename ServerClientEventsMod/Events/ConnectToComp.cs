using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerConnectToCompEvent : PathfinderEvent
{
    public Computer Computer { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }

    public PlayerConnectToCompEvent(Computer computer, OS os)
    {
        Computer = computer;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
    }
}

internal static class ConnectDetector
{
    private static string _lastConnectedIP;

    [FrameDetect]
    internal static void Detect(OS os)
    {
        string currentIP = os.connectedComp?.ip;

        if (_lastConnectedIP == currentIP)
            return;

        if (_lastConnectedIP != null)
        {
            EventManager<PlayerDisconnectFromCompEvent>.InvokeAll(
                new PlayerDisconnectFromCompEvent(_lastConnectedIP, os)
            );
        }

        if (currentIP != null && os.connectedComp != null)
        {
            EventManager<PlayerConnectToCompEvent>.InvokeAll(
                new PlayerConnectToCompEvent(os.connectedComp, os)
            );
        }

        _lastConnectedIP = currentIP;
    }
}
