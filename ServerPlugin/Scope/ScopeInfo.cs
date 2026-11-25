using System;

namespace ServerPlugin;

/// <summary>
/// Action / Condition / Goal / Mission 的执行范围信息。
/// </summary>
public class ScopeInfo
{
    /// <summary>"PUBLIC" 表示所有玩家，否则为玩家标识符</summary>
    public string Players { get; set; } = "PUBLIC";

    /// <summary>来源组名：空=Globle，非空=Private下的group name</summary>
    public string Group { get; set; } = "";

    public bool IsPublic =>
        string.Equals(Players, "PUBLIC", StringComparison.OrdinalIgnoreCase);
    public bool IsPrivate => !IsPublic;

    public bool Allows(string playerGlobleName,
                       string playerPrivateGroup = "",
                       string playerPrivateName = "")
    {
        if (IsPublic) return true;
        if (string.IsNullOrEmpty(Group))
            return string.Equals(Players, playerGlobleName,
                                 StringComparison.OrdinalIgnoreCase);
        return string.Equals(Group, playerPrivateGroup,
                             StringComparison.OrdinalIgnoreCase)
            && string.Equals(Players, playerPrivateName,
                             StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() =>
        IsPublic ? "PUBLIC"
        : string.IsNullOrEmpty(Group) ? $"Players={Players}"
        : $"Group={Group}, Players={Players}";
}
