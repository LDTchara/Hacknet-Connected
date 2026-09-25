using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerNodeIPChangedEvent : PathfinderEvent
{
    public Computer Computer { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    public string OldIP { get; }
    public string NewIP { get; }

    public PlayerNodeIPChangedEvent(Computer computer, OS os, string oldIP, string newIP)
    {
        Computer = computer;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        OldIP = oldIP;
        NewIP = newIP;
    }
}
