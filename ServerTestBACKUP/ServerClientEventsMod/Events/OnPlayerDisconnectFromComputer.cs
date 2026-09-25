using Hacknet;
using Pathfinder.Event;

namespace ServerClientEventsMod.Events;

[EventAutoSubscribe]
public class PlayerDisconnectFromCompEvent : PathfinderEvent
{
    public string IPFrom { get; }
    public OS Os { get; }
    public string CurrentPlayerIP { get; }

    public PlayerDisconnectFromCompEvent(string ipFrom, OS os)
    {
        IPFrom = ipFrom;
        Os = os;
        CurrentPlayerIP = os.thisComputer.ip;
    }
}
