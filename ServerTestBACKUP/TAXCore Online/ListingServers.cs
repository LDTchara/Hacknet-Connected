using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TAXCoreOnline;
using Hacknet;
using Pathfinder;
using System.Xml.Linq;
using System.Xml;
using TAXCoreOnline.LoadAndSave.Client;

namespace TAXCoreOnline
{
    public class ListingServers
    {
        public static void loadServersFromXml()
        {
            string ServerConfigPath = System.IO.Path.Combine(TAXCoreOnline.EnvPath, "OnlineSaves", "SaveServers.xml");
            Console.WriteLine("Loading server config from: " + ServerConfigPath);
            if (!System.IO.File.Exists(ServerConfigPath))
            {
                Console.WriteLine("Server config file not found: " + ServerConfigPath);
                File.Create(ServerConfigPath).Close();
                return;
            }
            try
            {
                XDocument doc = XDocument.Load(ServerConfigPath);
                var serversElement = doc.Root?.Element("Servers");
                if (serversElement == null)
                {
                    Console.WriteLine("No <Servers> element found in config.");
                    return;
                }
                foreach (var serverElem in serversElement.Elements("Server"))
                {
                    string localID = serverElem.Attribute("LocalID")?.Value;
                    if (string.IsNullOrEmpty(localID))
                    {
                        Console.WriteLine("Server element missing LocalID attribute, skipping.");
                        continue;
                    }
                    var server = new AvailableServer
                    {
                        LocalID = localID,
                        HostName = serverElem.Element("HostName")?.Value ?? "",
                        IP = serverElem.Element("IP")?.Value ?? "",
                        Port = int.Parse(serverElem.Element("Port")?.Value ?? "0"),
                        Accounts = new HashSet<ClientAccountStructure>()
                    };

                    var accountsElement = serverElem.Element("Accounts");
                    if (accountsElement != null)
                    {
                        foreach (var accountElem in accountsElement.Elements("account"))
                        {
                            string accountId = accountElem.Attribute("id")?.Value ?? "";
                            string saveFilePath = accountElem.Attribute("SaveFilePath")?.Value ?? "";

                            // ===== 修改点：直接从 XML 属性读取元数据，不再依赖存档文件 =====
                            string name = accountElem.Attribute("Name")?.Value ?? "";
                            string gameUsername = accountElem.Attribute("GameUsername")?.Value ?? "";
                            string password = accountElem.Attribute("Password")?.Value ?? "";

                            // 创建轻量级账户对象，仅填充基本信息（存档数据在登录时加载）
                            var account = new ClientAccountStructure
                            {
                                Id = accountId,
                                SaveFilePath = saveFilePath,
                                Name = name,
                                GameUsername = gameUsername,
                                Password = password,
                                Flags = "needs_initial_save",  // 标记尚未加载完整存档
                                ShowableNodes = new List<int>(),
                                Missions = new HashSet<ActiveMission>(),
                                BranchMissions = new HashSet<ActiveMission>()
                            };
                            server.Accounts.Add(account);
                        }
                    }

                    if (TAXCoreOnline.IDToServers.ContainsKey(localID))
                    {
                        Console.WriteLine($"Server LocalID '{localID}' already exists, overwriting.");
                        TAXCoreOnline.IDToServers[localID] = server;
                    }
                    else
                    {
                        TAXCoreOnline.IDToServers.Add(localID, server);
                    }
                }
                Console.WriteLine($"Loaded {TAXCoreOnline.IDToServers.Count} servers from config file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading server config: {ex.Message}");
            }
        }
    }
}
