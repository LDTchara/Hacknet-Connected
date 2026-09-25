using Hacknet;
using Hacknet.Extensions;
using HacknetHotReplace.Server.Entity;
using Pathfinder.Event;
using Pathfinder.Event.Saving;
using Pathfinder.Replacements;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace HacknetHotReplace.Server.Handler.Impl
{
    public class PrintOsMsgHandler : IMessageHandler
    {
        public string SupportMsgType => "PrintOS";

        public Task<object> HandleMessage(RequestMessage msg)
        {
            if (ExtensionLoader.ActiveExtensionInfo == null)
            {
                throw new InvalidOperationException("Please print os after entering the extension");
            }

            if (OS.currentInstance == null)
            {
                throw new InvalidOperationException("Current os is null, please print os after entering the extension");
            }

            var os = OS.currentInstance;
            var builder = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true
            };
            using (var writer = XmlWriter.Create(builder, settings))
            {
                var saveElement = SaveWriter.GetHacknetSaveElement(os);
                saveElement.Add(SaveWriter.GetDLCSaveElement(os));
                saveElement.Add(SaveWriter.GetFlagsSaveElement(os.Flags));
                saveElement.Add(SaveWriter.GetMissionSaveElement(os.currentMission));

                var branchMissionsTag = new XElement("branchMissions");
                foreach (var branch in os.branchMissions)
                    branchMissionsTag.Add(SaveWriter.GetMissionSaveElement(branch));

                saveElement.Add(branchMissionsTag);

                saveElement.Add(SaveWriter.GetAllFactionsSaveElement(os.allFactions));

                var otherTag = new XElement("other");
                otherTag.SetAttributeValue("music", MusicManager.currentSongName);
                otherTag.SetAttributeValue("homeNode", os.homeNodeID);
                otherTag.SetAttributeValue("homeAssetsNode", os.homeAssetServerID);
                saveElement.Add(otherTag);

                try
                {
                    EventManager<SaveEvent>.InvokeAll(new SaveEvent(os, saveElement, null));
                }
                catch (Exception)
                {
                    // ignore
                }

                writer.WriteStartDocument();

                saveElement.WriteTo(writer);

                writer.WriteEndDocument();
            }

            return Task.FromResult<object>(builder.ToString());
        }
    }
}
