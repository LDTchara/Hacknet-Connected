using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hacknet;
using Pathfinder;
using TAXCoreOnline;
using System.IO;
using System.Xml;
using System.Threading.Tasks;
using Hacknet.Factions;
namespace TAXCoreOnline.LoadAndSave.Client;
public class ClientAccountStructure
{
    public string SaveFilePath { get; set; }
    public string Name { get; set; }
    public string GameUsername { get; set; }
    public string Password { get; set; }
    public string Id { get; set; }      // id -> Id
    public string ExtensionName { get; set; }
    public string Flags { get; set; }
    public CustomFaction CurrentFaction { get; set; }

    public Computer PlayerNode { get; set; }
    public List<int> ShowableNodes { get; set; }
    public HashSet<ActiveMission> Missions { get; set; }
    public HashSet<ActiveMission> BranchMissions { get; set; }
    public AllFactions AllFactions { get; set; }
    public string Music { get; set; }
    public string HomeNode { get; set; }
    public string HomeAssetsNode { get; set; }
}
