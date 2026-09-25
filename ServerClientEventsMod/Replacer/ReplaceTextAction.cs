using System;
using Hacknet;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace ExtraEventsPlugin;

/// <summary>
/// XML: &lt;Replace Org="#ASDFASFF#" Curr="asdfassdf" /&gt;
/// Delay and DelayHost are inherited from DelayablePathfinderAction.
/// </summary>
public sealed class ReplaceTextAction : DelayablePathfinderAction
{
    [XMLStorage]
    public string Org;

    [XMLStorage]
    public string Curr;

    public override void LoadFromXml(ElementInfo info)
    {
        base.LoadFromXml(info);

        if (Org == null)
            throw new FormatException("Replace: missing required Org attribute.");
        if (Curr == null)
            throw new FormatException("Replace: missing required Curr attribute.");
    }

    public override void Trigger(OS os)
    {
        ReplacementCatalog.Add(Org, Curr);
        RuntimeObjectRewriter.Apply(os);
    }
}
