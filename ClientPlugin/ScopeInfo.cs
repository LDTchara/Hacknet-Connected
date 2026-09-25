using System;

namespace ClientPlugin;

/// <summary>
/// Action / Condition / Goal / Mission 的执行范围信息。
/// </summary>
public class ScopeInfo
{
    public string Players { get; set; } = "PUBLIC";
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
