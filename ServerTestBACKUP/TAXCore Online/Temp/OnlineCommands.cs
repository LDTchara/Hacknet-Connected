using Hacknet;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TAXCoreOnline
{
    public static class OnlineCommands
    {
        public static void SendChatMessage(OS os, string[] args)
        {
            if (args.Length < 2)
            {
                os.write("Usage: chat [message]");
                return;
            }

            string message = string.Join(" ", args.Skip(1));
            
            // Basic filtering/formatting similar to ZeroDayToolKit
            message = message.Replace("\\n", "\n");
            
            // Check for attachments (simplified version of ZeroDayToolKit logic)
            var m1 = new Regex(@"^\s*([^@]+?)\s*@\s*([^@]+?)\s*$").Match(message);
            if (m1.Success) 
            {
                message = $"!ATTACHMENT:link#%#{m1.Groups[1]}#%#{m1.Groups[2]}";
            }

            TAXCoreOnline.SendToServer("CHAT:" + message);
            os.write($"[YOU]: {message}");
        }
    }
}
