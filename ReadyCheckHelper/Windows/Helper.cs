﻿using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace ReadyCheckHelper.Windows;

public static class Helper
{
    public static void ImGuiHelpMarker(string description, bool sameLine = true, string marker = "(?)")
    {
        if(sameLine)
            ImGui.SameLine();

        ImGui.TextDisabled(marker);
        if (ImGui.IsItemHovered())
        {
            using (ImRaii.Tooltip())
            {
                ImGui.PushTextWrapPos(350.0f * ImGuiHelpers.GlobalScale);
                ImGui.TextUnformatted(description);
                ImGui.PopTextWrapPos();
            }
        }
    }
}