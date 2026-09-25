using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerNodeVisibilityEvent : PathfinderEvent
{
    public Computer Computer { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }
    public bool Visible { get; }

    public PlayerNodeVisibilityEvent(Computer computer, OS os, bool visible)
    {
        Computer = computer;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
        Visible = visible;
    }
}
