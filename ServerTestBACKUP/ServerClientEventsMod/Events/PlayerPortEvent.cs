using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerPortEvent : PathfinderEvent
{
    public Computer Computer { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    public int DisplayPort { get; }
    public int CodePort { get; }
    public bool IsOpen { get; }

    public PlayerPortEvent(Computer computer, OS os, int displayPort, int codePort, bool isOpen)
    {
        Computer = computer;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        DisplayPort = displayPort;
        CodePort = codePort;
        IsOpen = isOpen;
    }
}

internal static class PortDetector
{
    private static string _lastComputerIP;
    private static List<byte> _lastPortsOpen;

    [FrameDetect]
    internal static void Detect(OS os)
    {
        Computer comp = os.connectedComp;
        if (comp == null || comp.ports == null || comp.portsOpen == null)
        {
            _lastComputerIP = null;
            _lastPortsOpen = null;
            return;
        }

        string ip = comp.ip;

        if (ip != _lastComputerIP || _lastPortsOpen == null)
        {
            _lastPortsOpen = new List<byte>(comp.portsOpen);
            _lastComputerIP = ip;
            return;
        }

        int count = Math.Min(comp.portsOpen.Count, _lastPortsOpen.Count);
        for (int i = 0; i < count; i++)
        {
            if (comp.portsOpen[i] != _lastPortsOpen[i])
            {
                bool isOpen = comp.portsOpen[i] > 0;
                int displayPort = comp.GetDisplayPortNumberFromCodePort(comp.ports[i]);

                EventManager<PlayerPortEvent>.InvokeAll(
                    new PlayerPortEvent(comp, os, displayPort, comp.ports[i], isOpen)
                );
            }
        }

        _lastPortsOpen = new List<byte>(comp.portsOpen);
    }
}
