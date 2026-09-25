using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Hacknet;
using HarmonyLib;

namespace TAXCoreOnline.LoadAndSave.Client
{
    [HarmonyPatch]
    public class ClientLoadFromOLSave
    {
        [HarmonyPatch(typeof(OS), nameof(OS.loadSaveFile))]
        [HarmonyPrefix]
        static void LoadOnlineSavePrefix(OS __instance)
        {
            Console.WriteLine("=== LoadOnlineSavePrefix CALLED ===");
            
            if (
                //__instance.SaveGameUserName.StartsWith("save_OL_") ||
                !(TAXCoreOnline.CurrentMachineStateEnum == TAXCoreOnline.CurrentMachineState.Offline))
            {
                string localID = TAXCoreOnline.CurrentAccount?.Id;
                string playerGameName = TAXCoreOnline.CurrentAccount?.GameUsername;

                if (string.IsNullOrEmpty(localID) || string.IsNullOrEmpty(playerGameName))
                    return;

                var cleanStream = LoadOnlineSaveAndUpdateAccount(localID, playerGameName);

                if (cleanStream == null)
                {
                    Console.WriteLine("Online save file not found. Loading aborted.");
                    __instance.ForceLoadOverrideStream = new MemoryStream(Encoding.Unicode.GetBytes("<Invalid/>"));
                    OS.TestingPassOnly = true;   // ★ 强制使用覆盖流
                    return;
                }

                __instance.ForceLoadOverrideStream = cleanStream;
                OS.TestingPassOnly = true;       // ★★★ 关键：必须设置，否则原方法忽略覆盖流 ★★★

            }

        }

        public static MemoryStream LoadOnlineSaveAndUpdateAccount(string localID, string playerGameName)
        {

            string serverName = TAXCoreOnline.IDToServers[localID].HostName;
            string filePath = Path.Combine(
                TAXCoreOnline.EnvPath, "OnlineSaves", "Client", serverName,
                $"save_OL_{playerGameName}.xml"
            );
            Console.WriteLine($"OL SavePath: {filePath}");
            Console.WriteLine($"File exists: {File.Exists(filePath)}");

            if (!File.Exists(filePath))
                return null;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var account = SavePatches.ReadFileStreamToAccount(fs);
                TAXCoreOnline.CurrentAccount = account;
            }

            string fileContent = File.ReadAllText(filePath, Encoding.Unicode);
            var doc = XDocument.Parse(fileContent);
            var root = doc.Root;

            root.Elements("Player").Remove();
            root.Elements("Name").Remove();
            root.Elements("GameUserName").Remove();
            root.Elements("Password").Remove();
            root.Elements("SaveFilePath").Remove();
            root.Elements("ExtensionName").Remove();
            root.Elements("AllFactions").Remove();

            var cleanStream = new MemoryStream();
            doc.Save(cleanStream);
            cleanStream.Position = 0;
            return cleanStream;
        }
    }
}
