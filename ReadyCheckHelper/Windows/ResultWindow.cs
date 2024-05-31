using System;
using System.Collections.Generic;
using System.Numerics;
using CheapLoc;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;

namespace ReadyCheckHelper.Windows;

public class ResultWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    private readonly IDalamudTextureWrap ReadyCheckIconTexture;
    private readonly IDalamudTextureWrap UnknownStatusIconTexture;
    private readonly IDalamudTextureWrap NotPresentIconTexture;

    public ResultWindow(Plugin plugin) : base($"{Loc.Localize("Window Title: Ready Check Results", "Latest Ready Check Results")}###Latest Ready Check Results")
    {
        Plugin = plugin;
        ReadyCheckIconTexture = Plugin.Texture.GetTextureFromGame("ui/uld/ReadyCheck_hr1.tex") ?? Plugin.Texture.GetTextureFromGame("ui/uld/ReadyCheck.tex");
        UnknownStatusIconTexture = Plugin.Texture.GetIcon(60072);
        NotPresentIconTexture = Plugin.Texture.GetIcon(61504);

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(180, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var list = Plugin.GetProcessedReadyCheckData();
        if (list != null)
        {
            //	We have to sort and reorganize this yet again because of how ImGui tables work ;_;
            list.Sort((a, b) => a.GroupIndex.CompareTo(b.GroupIndex));
            var tableList = new List<List<CorrelatedReadyCheckEntry>>();
            foreach (var player in list)
            {
                if (tableList.Count <= player.GroupIndex)
                    tableList.Add([]);

                tableList[player.GroupIndex].Add(player);
            }

            using var table = ImRaii.Table("###LatestReadyCheckResultsTable", tableList.Count);
            if (table)
            {
                for (var i = 0; i < 8; ++i)
                {
                    ImGui.TableNextRow();
                    for (var j = 0; j < tableList.Count; ++j)
                    {
                        ImGui.TableSetColumnIndex(j);
                        if (i < tableList[j].Count)
                        {
                            switch (tableList[j][i].ReadyState)
                            {
                                case AgentReadyCheck.ReadyCheckStatus.Ready:
                                    ImGui.Image(ReadyCheckIconTexture.ImGuiHandle, new Vector2(24), new Vector2(0.0f), new Vector2(0.5f, 1.0f));
                                    break;
                                case AgentReadyCheck.ReadyCheckStatus.NotReady:
                                    ImGui.Image(ReadyCheckIconTexture.ImGuiHandle, new Vector2(24), new Vector2(0.5f, 0.0f), new Vector2(1.0f));
                                    break;
                                case AgentReadyCheck.ReadyCheckStatus.MemberNotPresent:
                                    ImGui.Image(NotPresentIconTexture.ImGuiHandle, new Vector2(24));
                                    break;
                                default:
                                    ImGui.Image(UnknownStatusIconTexture.ImGuiHandle, new Vector2(24), new Vector2(0.0f), new Vector2(1.0f), new Vector4(0.0f));
                                    break;
                            }

                            ImGui.SameLine();
                            ImGui.Text(tableList[j][i].Name);
                        }
                        else
                        {
                            ImGui.Text(" ");
                        }
                    }
                }
            }
        }
        else
        {
            ImGui.Text(Loc.Localize("Placeholder: No Ready Check Results Exist", "No ready check has yet occurred."));
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        if (ImGui.Button($"{Loc.Localize("Button: Close", "Close")}###Close"))
            IsOpen = false;
    }
}