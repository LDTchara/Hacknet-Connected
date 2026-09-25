using BepInEx;
using BepInEx.Hacknet;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using Hacknet;
using Pathfinder.Command;
using LiteNetLib;
using System.Drawing.Text;
using TAXCoreOnline.LoadAndSave.Client;
using Hacknet.Factions;

namespace TAXCoreOnline;

[BepInPlugin(ModGUID, ModName, ModVer)]
public class TAXCoreOnline : HacknetPlugin
{
    public const string ModGUID = "com.DSL.TAXCoreOnline";
    public const string ModName = "TAXCore Online";
    public const string ModVer = "1.0.0";

    public static TcpClient Client;
    public static NetworkStream Stream;
    public static Dictionary<string, AvailableServer> IDToServers = new Dictionary<string, AvailableServer>();//AvailableServers FOR ALL SERVERS
    public static string Username;//从当前创建的账户获取
    public bool isHost=false;
    public bool isClient=false;
    public AvailableServer SelectedServer;//从当前连接的服务器获取
    public static CurrentMachineState CurrentMachineStateEnum;
    public static AvailableServer CurrentServer;//从当前连接的服务器获取
    public static ClientAccountStructure CurrentAccount;//从当前账户获取
    
    public static EventBasedNetListener listener = new EventBasedNetListener();
    public static NetManager client = new NetManager(listener);
    



    public struct Account
    {
        public string Name;
        public string GameUsername;
        public string Password;
        public string Id;
        public string ExtensionName;
        public string Flags;
        public FileStream SaveFileStream;
        public CustomFaction CurrentFaction;
        public Computer PlayerNode;
        public List<int> ShowableNodes;
        public HashSet<ActiveMission> Missions;
        public HashSet<ActiveMission> BranchMissions;
        public AllFactions AllFactions;
        public string Music;
        public string HomeNode;
        public string HomeAssetsNode;





    }
    
    
    //TODO: 连接服务器后初始化时获取当前账户和当前服务器信息和账户信息并存储一次CurrentAccount
    
    public static string EnvPath = Environment.CurrentDirectory;
    public enum CurrentMachineState
    {
        Host,
        Client,
        Offline
    }

    public override bool Load()
    {
        HarmonyInstance.PatchAll(typeof(TAXCoreOnline).Assembly);
        client.Start();

        //_________________
        //SavePatches.SaveAllServersConfigToFile();
        ListingServers.loadServersFromXml();
        foreach (var kvp in IDToServers)
        {
            Console.WriteLine($"Server ID: {kvp.Key}, HostName: {kvp.Value.HostName}, IP: {kvp.Value.IP}, Port: {kvp.Value.Port}");
        }
        //CommandManager.RegisterCommand("chat", OnlineCommands.SendChatMessage);
        //Host?
        CurrentMachineStateEnum = CheckMachineState(isHost, isClient);


        CurrentMachineStateEnum = CurrentMachineState.Client;
        //===========TEST=============



        /*=============================*/
        //Load Servers From XML


        return true;
    }

    public override bool Unload()
    {
        client.Stop();
        return true;
    }

    public CurrentMachineState CheckMachineState(bool isHost,bool isClient)
    {

        if (isHost && !isClient)
        {
            isHost = true;
            isClient = false;
            return CurrentMachineState.Host;
        }
        else if (!isHost && isClient)
        {
            isHost = false;
            isClient = true;
            return CurrentMachineState.Client;
        }
        else
        {
            isHost = false;
            isClient = false;
            return CurrentMachineState.Offline;
        }
        
    }
}
public class AvailableServer
{
    public string LocalID;
    public string HostName;
    public string IP;
    public int Port;
    public HashSet<ClientAccountStructure> Accounts; // No SAME!!!!!!
}

