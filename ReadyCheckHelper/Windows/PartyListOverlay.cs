using System;
using System.Numerics;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace ReadyCheckHelper.Windows;

public class PartyListOverlay : Window, IDisposable
{
    private readonly Plugin Plugin;

    public bool ReadyCheckValid;
    private readonly IDalamudTextureWrap ReadyCheckIconTexture;
    private readonly IDalamudTextureWrap NotPresentIconTexture;

    public PartyListOverlay(Plugin plugin) : base("##ReadyCheckOverlayWindow")
    {
        Plugin = plugin;
        ReadyCheckIconTexture = Plugin.Texture.GetTextureFromGame("ui/uld/ReadyCheck_hr1.tex") ??
                                Plugin.Texture.GetTextureFromGame("ui/uld/ReadyCheck.tex");
        NotPresentIconTexture = Plugin.Texture.GetIcon(61504);

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(180, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        ForceMainWindow = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoNav;
    }

    public void Dispose()
    {
    }

    public override void PreOpenCheck()
    {
        IsOpen = Plugin.DebugWindow.DrawPlaceholderData || (Plugin.Configuration.ShowReadyCheckOnPartyAllianceList && ReadyCheckValid);
    }

    public override void PreDraw()
    {
        Size = ImGuiHelpers.MainViewport.Size;
        Position = ImGuiHelpers.MainViewport.Pos;
    }

    public override unsafe void Draw()
    {
        var pPartyList = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("_PartyList");
        var pAlliance1List = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("_AllianceList1");
        var pAlliance2List = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("_AllianceList2");
        var pCrossWorldAllianceList = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("Alliance48");

        var drawList = ImGui.GetWindowDrawList();
        if (Plugin.DebugWindow.DrawPlaceholderData)
        {
            if ((nint)pPartyList != nint.Zero && pPartyList->IsVisible)
            {
                for (var i = 0; i < 8; ++i)
                    DrawOnPartyList(i, AgentReadyCheck.ReadyCheckStatus.Ready, pPartyList, drawList);
            }

            if ((nint)pAlliance1List != nint.Zero && pAlliance1List->IsVisible)
            {
                for (var i = 0; i < 8; ++i)
                    DrawOnAllianceList(i, AgentReadyCheck.ReadyCheckStatus.Ready, pAlliance1List, drawList);
            }

            if ((nint)pAlliance2List != nint.Zero && pAlliance2List->IsVisible)
            {
                for (var i = 0; i < 8; ++i)
                    DrawOnAllianceList(i, AgentReadyCheck.ReadyCheckStatus.Ready, pAlliance2List, drawList);
            }

            if ((nint)pCrossWorldAllianceList != nint.Zero && pCrossWorldAllianceList->IsVisible)
            {
                for (var j = 1; j < 6; ++j)
                for (var i = 0; i < 8; ++i)
                    DrawOnCrossWorldAllianceList(j, i, AgentReadyCheck.ReadyCheckStatus.Ready, pCrossWorldAllianceList,
                        drawList);
            }
        }
        else
        {
            var data = Plugin.GetProcessedReadyCheckData();
            if (data != null)
            {
                foreach (var result in data)
                {
                    var indices = MemoryHandler.GetHUDIndicesForChar(result.ContentID, result.ObjectID);
                    if (indices == null) continue;
                    switch (indices.Value.GroupNumber)
                    {
                        case 0:
                            DrawOnPartyList(indices.Value.PartyMemberIndex, result.ReadyState, pPartyList, drawList);
                            break;
                        case 1:
                            if (indices.Value.CrossWorld)
                                break; //***** TODO: Do something when crossworld alliances are fixed.
                            else
                                DrawOnAllianceList(indices.Value.PartyMemberIndex, result.ReadyState, pAlliance1List,
                                    drawList);
                            break;
                        case 2:
                            if (indices.Value.CrossWorld)
                                break; //***** TODO: Do something when crossworld alliances are fixed.
                            else
                                DrawOnAllianceList(indices.Value.PartyMemberIndex, result.ReadyState, pAlliance2List,
                                    drawList);
                            break;
                        default:
                            if (indices.Value.CrossWorld)
                                break; //***** TODO: Do something when crossworld alliances are fixed.
                            break;
                    }
                }
            }
        }
    }

    private unsafe void DrawOnPartyList(int listIndex, AgentReadyCheck.ReadyCheckStatus readyCheckState, AtkUnitBase* pPartyList, ImDrawListPtr drawList)
    {
        if (listIndex is < 0 or > 7)
            return;

        var partyMemberNodeIndex = 22 - listIndex;
        var iconNodeIndex = 4;
        var partyAlign = pPartyList->UldManager.NodeList[3]->Y;

        var pPartyMemberNode = pPartyList->UldManager.NodeListSize > partyMemberNodeIndex
            ? (AtkComponentNode*)pPartyList->UldManager.NodeList[partyMemberNodeIndex]
            : (AtkComponentNode*)nint.Zero;
        if ((nint)pPartyMemberNode == nint.Zero)
            return;

        var pIconNode = pPartyMemberNode->Component->UldManager.NodeListSize > iconNodeIndex
            ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex]
            : (AtkResNode*)nint.Zero;
        if ((nint)pIconNode == nint.Zero)
            return;

        //	Note: sub-nodes don't scale, so we have to account for the addon's scale.
        var iconOffset = (new Vector2(-7, -5) + Plugin.Configuration.PartyListIconOffset) * pPartyList->Scale;
        var iconSize = new Vector2(pIconNode->Width / 3.0f, pIconNode->Height / 3.0f) * Plugin.Configuration.PartyListIconScale * pPartyList->Scale;
        var iconPos = new Vector2(pPartyList->X + pPartyMemberNode->AtkResNode.X * pPartyList->Scale + pIconNode->X * pPartyList->Scale + pIconNode->Width * pPartyList->Scale / 2, pPartyList->Y + partyAlign + pPartyMemberNode->AtkResNode.Y * pPartyList->Scale + pIconNode->Y * pPartyList->Scale + pIconNode->Height * pPartyList->Scale / 2);
        iconPos += iconOffset;

        if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.NotReady)
            drawList.AddImage(ReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
        else if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.Ready)
            drawList.AddImage(ReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.0f, 0.0f), new Vector2(0.5f, 1.0f));
        else if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.MemberNotPresent)
            drawList.AddImage(NotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize);
    }

    private unsafe void DrawOnAllianceList(int listIndex, AgentReadyCheck.ReadyCheckStatus readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList)
    {
        if (listIndex is < 0 or > 7)
            return;

        var partyMemberNodeIndex = 9 - listIndex;
        var iconNodeIndex = 5;

        var pAllianceMemberNode = pAllianceList->UldManager.NodeListSize > partyMemberNodeIndex
            ? (AtkComponentNode*)pAllianceList->UldManager.NodeList[partyMemberNodeIndex]
            : (AtkComponentNode*)nint.Zero;
        if ((nint)pAllianceMemberNode == nint.Zero)
            return;

        var pIconNode = pAllianceMemberNode->Component->UldManager.NodeListSize > iconNodeIndex
            ? pAllianceMemberNode->Component->UldManager.NodeList[iconNodeIndex]
            : (AtkResNode*)nint.Zero;
        if ((nint)pIconNode == nint.Zero)
            return;

        var iconOffset = (new Vector2(0, 0) + Plugin.Configuration.AllianceListIconOffset) * pAllianceList->Scale;
        var iconSize = new Vector2(pIconNode->Width / 3.0f, pIconNode->Height / 3.0f) * Plugin.Configuration.AllianceListIconScale * pAllianceList->Scale;
        var iconPos = new Vector2(pAllianceList->X + pAllianceMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2, pAllianceList->Y + pAllianceMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2);
        iconPos += iconOffset;

        if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.NotReady)
            drawList.AddImage(ReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
        else if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.Ready)
            drawList.AddImage(ReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.0f), new Vector2(0.5f, 1.0f));
        else if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.MemberNotPresent)
            drawList.AddImage(NotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize);
    }

    private unsafe void DrawOnCrossWorldAllianceList(int allianceIndex, int partyMemberIndex, AgentReadyCheck.ReadyCheckStatus readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList)
    {
        if (allianceIndex is < 1 or > 5)
            return;

        if (partyMemberIndex is < 0 or > 7)
            return;

        var allianceNodeIndex = 8 - allianceIndex;
        var partyMemberNodeIndex = 8 - partyMemberIndex;
        var iconNodeIndex = 2;

        // Check if it is loaded, else this could lead to a crash
        if (pAllianceList->UldManager.LoadedState != AtkLoadState.Loaded)
            return;

        var pAllianceNode = pAllianceList->UldManager.NodeListSize > allianceNodeIndex
            ? (AtkComponentNode*)pAllianceList->UldManager.NodeList[allianceNodeIndex]
            : (AtkComponentNode*)nint.Zero;
        if ((nint)pAllianceNode == nint.Zero)
            return;

        var pPartyMemberNode = pAllianceNode->Component->UldManager.NodeListSize > partyMemberNodeIndex
            ? (AtkComponentNode*)pAllianceNode->Component->UldManager.NodeList[partyMemberNodeIndex]
            : (AtkComponentNode*)nint.Zero;
        if ((nint)pPartyMemberNode == nint.Zero)
            return;

        var pIconNode = pPartyMemberNode->Component->UldManager.NodeListSize > iconNodeIndex
            ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex]
            : (AtkResNode*)nint.Zero;
        if ((nint)pIconNode == nint.Zero)
            return;

        var iconOffset = (new Vector2(0, 0) + Plugin.Configuration.CrossWorldAllianceListIconOffset) * pAllianceList->Scale;
        var iconSize = new Vector2(pIconNode->Width / 2.0f, pIconNode->Height / 2.0f) * Plugin.Configuration.CrossWorldAllianceListIconScale * pAllianceList->Scale;
        var iconPos = new Vector2(pAllianceList->X + pAllianceNode->AtkResNode.X * pAllianceList->Scale + pPartyMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2, pAllianceList->Y + pAllianceNode->AtkResNode.Y * pAllianceList->Scale + pPartyMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2);
        iconPos += iconOffset;

        if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.NotReady)
            drawList.AddImage(ReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
        else if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.Ready)
            drawList.AddImage(ReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.0f, 0.0f), new Vector2(0.5f, 1.0f));
        else if (readyCheckState == AgentReadyCheck.ReadyCheckStatus.MemberNotPresent)
            drawList.AddImage(NotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize);
    }

    public void ShowReadyCheckOverlay()
    {
        ReadyCheckValid = true;
    }

    public void InvalidateReadyCheck()
    {
        ReadyCheckValid = false;
    }
}