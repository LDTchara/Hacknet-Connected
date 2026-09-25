using System;
using Hacknet;
using HarmonyLib;

namespace ClientPlugin;

/// <summary>
/// 修复原版 MailServer.addMail 对"无账户收件人"抛 NRE 的 bug：
///
/// 原版逻辑（MailServer.cs:250-273）：
///   1. 先触发 responders[i].mailReceived(mail, userTo)（邮件提示音在这里）
///   2. 再找 accounts.folders 里收件人的账户文件夹
///   3. 找不到 → throw NullReferenceException
///
/// 当扩展启动邮件（SendStartingEmailForActiveExtensionNextFrame）的收件人在
/// 邮件服务器上没有账户时，ActionDelayer.Pump() 每帧重试该 action →
/// 每帧响一次提示音 + 每帧 NRE，形成"邮件提示音风暴"。
///
/// 本补丁：收件人不在账户列表时直接跳过原方法（静默丢弃），不崩、不响。
/// </summary>
[HarmonyPatch(typeof(MailServer), "addMail")]
internal static class MailServerAddMailPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(MailServer __instance, string userTo)
    {
        if (__instance.accounts?.folders != null)
        {
            foreach (var f in __instance.accounts.folders)
            {
                if (f != null && f.name.Equals(userTo, StringComparison.OrdinalIgnoreCase))
                    return true; // 有账户，放行原方法
            }
        }

        Console.WriteLine($"[MailServerPatch] 收件人 \"{userTo}\" 无账户，邮件已丢弃（server={__instance.comp?.idName}）");
        return false;
    }
}
