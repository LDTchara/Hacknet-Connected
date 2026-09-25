using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using Hacknet;
using Hacknet.PlatformAPI.Storage;
using Pathfinder.Util;
using Hacknet.Extensions;
using System.Xml;
using System.Security.Principal;
using Hacknet.Factions;
using Pathfinder.Replacements;
using System.Reflection;
using System.Runtime.Remoting.Messaging;

namespace TAXCoreOnline.LoadAndSave.Client;
[HarmonyPatch]
public static class SavePatches
{   
    //存储副本用于下一部分的转化读取
    private static string OnlineSaveDir => Path.Combine(TAXCoreOnline.EnvPath, "OnlineSaves", "Client",TAXCoreOnline.CurrentServer.HostName);

    [HarmonyPatch(typeof(SaveFileManager), nameof(SaveFileManager.WriteSaveData))]
    [HarmonyPostfix]
    static void WriteSaveDataPostfix(string saveData, string playerID)
    {
       if(!Directory.Exists(OnlineSaveDir))
       {
            Directory.CreateDirectory(OnlineSaveDir);

       }
       File.WriteAllText(Path.Combine(OnlineSaveDir,"save_" + playerID + ".xml"), saveData, Encoding.Unicode);
       SaveToClientAccountStructure(OS.currentInstance);
       SaveToFile(Path.Combine(OnlineSaveDir, "save_" + playerID + ".xml"),
           Path.Combine(OnlineSaveDir, "save_OL_" + playerID + ".xml"),playerID, TAXCoreOnline.CurrentAccount.GameUsername,
                  TAXCoreOnline.CurrentAccount.GameUsername, TAXCoreOnline.CurrentAccount.Password);
        SaveAllServersConfigToFile();
    }

    public static void SaveToFile(string Orgfile,string Destfile, string playerId, string playerName,
                               string gameUsername, string password)
    {
        XDocument doc;
        using (var reader = new StreamReader(Orgfile, Encoding.Unicode))
        {
            doc = XDocument.Load(reader);
        }
        XElement root = doc.Root;

        // 修改根标签的 Username 属性为 gameUsername
        XAttribute usernameAttr = root.Attribute("Username");
        if (usernameAttr != null)
            usernameAttr.Value = gameUsername;
        else
            root.Add(new XAttribute("Username", gameUsername));

        // 移除已存在的同名子元素（如果有），避免重复添加
        root.Elements("Player").Remove();
        root.Elements("Name").Remove();
        root.Elements("GameUserName").Remove();
        root.Elements("Password").Remove();
        root.Elements("SaveFilePath").Remove();
        root.Elements("ExtensionName").Remove();
        // 添加新子元素（按顺序）
        root.Add(new XElement("Player", new XAttribute("id", playerId)));
        root.Add(new XElement("Name", playerName));
        root.Add(new XElement("GameUserName", gameUsername));
        root.Add(new XElement("Password", password));
        root.Add(new XElement("SaveFilePath", Destfile));
        root.Add(new XElement("ExtensionName",Hacknet.Extensions.ExtensionLoader.ActiveExtensionInfo.Name??""));
        // 保存到目标文件
        using (var writer = new FileStream(Destfile, FileMode.Create, FileAccess.Write))
        {
            doc.Save(writer);
        }


    }

    public static void SaveAllServersConfigToFile()
    {
        string saveDir = Path.Combine(TAXCoreOnline.EnvPath, "OnlineSaves");
        Directory.CreateDirectory(saveDir);
        string filePath = Path.Combine(saveDir, "SaveServers.xml");
        XDocument doc;
        if (File.Exists(filePath))
        {
            doc = XDocument.Load(filePath);
            doc.Root?.Element("Servers")?.Remove();
            if (doc.Root?.Name != "TAXCoreOnlineStorage")
                doc = new XDocument(new XElement("TAXCoreOnlineStorage"));
        }
        else
        {
            doc = new XDocument(new XElement("TAXCoreOnlineStorage"));
        }
        XElement serversElement = new XElement("Servers");
        foreach (var kvp in TAXCoreOnline.IDToServers)
        {
            var server = kvp.Value;
            XElement serverElement = new XElement("Server",
                new XAttribute("LocalID", server.LocalID ?? ""),
                new XElement("HostName", server.HostName ?? ""),
                new XElement("IP", server.IP ?? ""),
                new XElement("Port", server.Port),
                new XElement("Accounts")
            );
            XElement accountsElement = serverElement.Element("Accounts");
            if (server.Accounts != null)
            {
                foreach (var account in server.Accounts)
                {
                    string accountSavePath = Path.Combine(saveDir, "Client", server.HostName,
                        $"save_OL_{account.GameUsername}.xml");
                    string accountDir = Path.GetDirectoryName(accountSavePath);
                    if (!Directory.Exists(accountDir))
                    {
                        Directory.CreateDirectory(accountDir);
                        Console.WriteLine($"Created directory for account save: {accountDir}");
                    }
                    // ===== 修改点：添加 Name、GameUsername、Password 属性 =====
                    XElement accountElement = new XElement("account",
                        new XAttribute("id", account.Id ?? ""),
                        new XAttribute("SaveFilePath", accountSavePath),
                        new XAttribute("Name", account.Name ?? ""),
                        new XAttribute("GameUsername", account.GameUsername ?? ""),
                        new XAttribute("Password", account.Password ?? "")
                    );
                    accountsElement.Add(accountElement);
                }
            }
            serversElement.Add(serverElement);
        }
        doc.Root.Add(serversElement);
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        };
        using (var writer = XmlWriter.Create(filePath, settings))
        {
            doc.Save(writer);
        }
    }

    public static FileStream ReadFromSavedOLFileToClientAccountFile(string LoaclID, string playerGameName)
    {
        string servername = TAXCoreOnline.IDToServers[LoaclID].HostName;
        string savefileDir = Path.Combine(TAXCoreOnline.EnvPath, "OnlineSaves", "Client", servername, "save_OL_" + playerGameName + ".xml");
        FileStream fs = new FileStream(savefileDir, FileMode.Open, FileAccess.Read);



        return fs;
    }


    public static ClientAccountStructure ReadFileStreamToAccount(FileStream OLSaveFileStream)
    {

        OS os = OS.currentInstance;
        ClientAccountStructure account = new();
        StreamReader Srdr = new StreamReader(OLSaveFileStream,Encoding.Unicode);
        XmlReader rdr = XmlReader.Create(Srdr);
        while (rdr.Read())
        {
            if (rdr.NodeType != XmlNodeType.Element) continue;
            switch(rdr.Name)
            {
                case "Name":
                    account.Name = rdr.ReadElementContentAsString() ?? "";
                    break;

                case "GameUsername":
                    account.GameUsername = rdr.ReadElementContentAsString() ?? "";
                    break;
                case "Password":
                    account.Password = rdr.ReadElementContentAsString() ?? "";
                    break;
                case "SaveFilePath":
                    account.SaveFilePath = rdr.ReadElementContentAsString() ?? "";
                    break;
                case "Flags":
                    // 节点内容为逗号分隔的标记字符串
                    account.Flags = rdr.ReadElementContentAsString() ?? "";
                    break;

                case "Player":
                    // 读取 <Player id="1"> 中的 id 属性
                    if (rdr.MoveToAttribute("id"))
                        account.Id = rdr.ReadContentAsString();
                    else account.Id = "";
                    break;
                case "other":
                    // 读取属性：music, homeNode, homeAssetsNode
                    if (rdr.MoveToAttribute("music"))
                        account.Music = rdr.ReadContentAsString();
                    else account.Music = "";
                    if (rdr.MoveToAttribute("homeNode"))
                        account.HomeNode = rdr.ReadContentAsString();
                    else account.HomeNode = "";
                    if (rdr.MoveToAttribute("homeAssetsNode"))
                        account.HomeAssetsNode = rdr.ReadContentAsString();
                    else account.HomeAssetsNode = "";
                    break;
                case "visible":
                    // 读取 <visible> 元素的内容，例如 "0 2"
                    string content = rdr.ReadElementContentAsString();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        // 按空格分割，忽略空项，转为整数
                        var numbers = content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                             .Select(int.Parse)
                                             .ToList();
                        account.ShowableNodes = numbers;
                    }
                    else
                    {
                        account.ShowableNodes = new List<int>();
                    }
                    break;
                
                case "ExtensionName":
                    account.ExtensionName = rdr.ReadElementContentAsString()?? "";
                    break;
                case "AllFactions":
                    account.AllFactions = AllFactions.loadFromSave(rdr)??null;
                    account.CurrentFaction = (CustomFaction)CustomFaction.loadFromSave(rdr)??null;
                    break;
                
                case "missions":
                    {
                        var missions = new HashSet<ActiveMission>();
                        while (rdr.Read() && rdr.Name == "mission")
                        {
                            var mission = ActiveMission.load(rdr) as ActiveMission;
                            if (mission != null)
                                missions.Add(mission);
                        }
                        account.Missions = missions;
                        break;
                    }
                case "branchMissions":
                    {
                        var branchMissions = new HashSet<ActiveMission>();
                        while (rdr.Read() && rdr.Name == "branchMissions")
                        {
                            var mission = ActiveMission.load(rdr) as ActiveMission;
                            if (mission != null)
                                branchMissions.Add(mission);
                        }
                        account.BranchMissions = branchMissions;
                        break;
                    }
            }

        }
        account.PlayerNode = Programs.getComputer(os, "#PLAYER_IP#".Filter()) ?? new Computer("","",new Microsoft.Xna.Framework.Vector2(),0,1,os);


        return account;
    }






    /*SaveToClientAccountStructure*/
    public static void SaveToClientAccountStructure(OS os)
    {
        // 这里你需要根据你的保存数据格式来解析并填充 ClientAccountStructure 的属性
        // 这只是一个示例，具体实现取决于你的保存数据结构
        os = OS.currentInstance; // 获取当前的 OS 实例
        ClientAccountStructure account = new ClientAccountStructure();

        account.Name = TAXCoreOnline.CurrentAccount.Name;
        account.GameUsername = "#PLAYERNAME#".Filter();
        for(int i = 0; i < os.branchMissions.Count; i++)
        {
            account.BranchMissions.Add(os.branchMissions[i]);
        }
        account.ExtensionName = ExtensionLoader.ActiveExtensionInfo.Name;
        account.Id = TAXCoreOnline.CurrentAccount.Id;
        account.Flags = string.Join(",", os.Flags);
        account.SaveFilePath = Path.Combine(Path.Combine(TAXCoreOnline.EnvPath, "OnlineSaves", "Client", TAXCoreOnline.CurrentServer.HostName, TAXCoreOnline.CurrentAccount.Name), "save_" + "PLAYERNAME".Filter() + ".xml");
        account.ShowableNodes = os.netMap.visibleNodes;
        account.PlayerNode = Programs.getComputer(os,"#PLAYER_IP#".Filter());
        account.Password = TAXCoreOnline.CurrentAccount.Password;
        using (var fileStream = new StreamReader(
    Path.Combine(OnlineSaveDir, "save_" + "#PLAYERNAME#".Filter() + ".xml"),
    Encoding.Unicode))
        using (var rdr = XmlReader.Create(fileStream))
        {
            while (rdr.Read())
            {
                if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "other")
                {
                    string musicpath = rdr.GetAttribute("music");
                    string homeNode = rdr.GetAttribute("homeNode");
                    string homeAssetsNode = rdr.GetAttribute("homeAssetsNode");
                    if (!string.IsNullOrEmpty(musicpath))
                    {
                        account.Music = musicpath;
                    }
                    if (!string.IsNullOrEmpty(homeNode))
                    {
                        account.HomeNode = homeNode;
                    }
                    if (!string.IsNullOrEmpty(homeAssetsNode))
                    {
                        account.HomeAssetsNode = homeAssetsNode;
                    }
                }
            }
        }
            
        // 最后将解析后的账户结构赋值给当前账户
        TAXCoreOnline.CurrentAccount = account;
    }


}

