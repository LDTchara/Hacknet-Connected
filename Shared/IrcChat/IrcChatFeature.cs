using System;
using System.Linq;
using Hacknet;
using HNC.Chat;
using Pathfinder.Action;
using Pathfinder.Command;
using Pathfinder.Event;
using Pathfinder.Event.Loading;
using Pathfinder.Mission;

namespace HNC.IrcChat;

/// <summary>
/// IRC / DHS 发消息功能的入口。
/// 输入栏本身由 [HarmonyPatch] 的 IrcInputBar 提供，随各插件的 PatchAll 生效；这里只注册 sendto 命令。
/// </summary>
internal static class IrcChatFeature
{
    public static void Register()
    {
        // 禁言状态放进存档标记里
        ChatBus.MuteStore = new FlagMuteStore();

        CommandManager.RegisterCommand("sendto", HandleSendTo);

        // XML 标签：<BanChatInput/> <UnbanChatInput/>（动作）  <ChatSent/>（条件）  <goal type="chatsent"/>（目标）
        TryRegister("BanChatInput", () => ActionManager.RegisterAction<BanChatInputAction>("BanChatInput"));
        TryRegister("UnbanChatInput", () => ActionManager.RegisterAction<UnbanChatInputAction>("UnbanChatInput"));
        TryRegister("ChatSent", () => ConditionManager.RegisterCondition<ChatSentCondition>("ChatSent"));
        TryRegister("chatsent", () => GoalManager.RegisterGoal<ChatSentGoal>("chatsent"));

        // 换存档 / 新世界：内存里的聊天历史属于上一个世界
        EventManager<OSLoadedEvent>.AddHandler(_ => ChatBus.Clear());
    }

    private static void TryRegister(string name, Action register)
    {
        try { register(); }
        catch (Exception ex) { Console.WriteLine($"[IrcChat] 注册 {name} 失败: {ex.Message}"); }
    }

    // sendto <消息...>：发到你正在看的（或当前连接的电脑上的）IRC / DHS
    //   @everyone / @用户名    提及
    //   @IP                   附上这个节点
    //   @IP -p                附上节点和你已记录的账户密码
    //   @IP -p 账户 密码       手动指定（不对则不发送）
    private static void HandleSendTo(OS os, string[] args)
    {
        if (args.Length < 2)
        {
            os.write("用法: sendto <消息>");
            os.write("  @everyone / @用户名     提及");
            os.write("  @IP                    附上这个节点");
            os.write("  @IP -p                 附上节点和你已记录的账户密码");
            os.write("  @IP -p 账户 密码        手动指定账户密码（不对则不发送）");
            return;
        }

        var sys = IrcSender.FindCurrent(os);
        if (sys == null)
        {
            os.write("[!] 找不到可以发送的 IRC / DHS。请先连接到有 IRC 或 DHS 的节点，并打开它的界面。");
            return;
        }

        string message = string.Join(" ", args.Skip(1));
        if (IrcSender.TrySend(sys, os, message))
            os.write("[IRC] 已发送");
    }
}
