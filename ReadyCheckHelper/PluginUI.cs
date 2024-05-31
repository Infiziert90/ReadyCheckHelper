using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Globalization;
using System.IO;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Interface;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using CheapLoc;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ReadyCheckHelper
{
	public class PluginUI : IDisposable
	{
		private Plugin Plugin;
		private Configuration Configuration;

		//	Construction
		public PluginUI( Plugin plugin, Configuration configuration)
		{
			Plugin = plugin;
			Configuration = configuration;
		}

		//	Destruction
		public void Dispose() { }

		public void Initialize()
		{
			var classJobSheet = Plugin.DataManager.GetExcelSheet<ClassJob>()!;
			foreach( var job in classJobSheet.ToList() )
				JobDict.Add( job.RowId, job.Abbreviation );

			mReadyCheckIconTexture		??= Plugin.Texture.GetTextureFromGame( "ui/uld/ReadyCheck_hr1.tex" ) ?? Plugin.Texture.GetTextureFromGame( "ui/uld/ReadyCheck.tex" );
			mUnknownStatusIconTexture	??= Plugin.Texture.GetIcon( 60072 );
			mNotPresentIconTexture		??= Plugin.Texture.GetIcon( 61504 );
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawSettingsWindow();
			DrawReadyCheckResultsWindow();
			DrawDebugWindow();
			DrawDebugProcessedWindow();

			//	Draw other UI stuff.
			DrawOnPartyAllianceLists();
		}

		private void DrawSettingsWindow()
		{
			if( !SettingsWindowVisible )
			{
				return;
			}

			if( ImGui.Begin( Loc.Localize( "Window Title: Config", "Ready Check Helper Settings" ) + "###Ready Check Helper Settings",
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Checkbox( Loc.Localize( "Config Option: Print Names of Unready in Chat", "Show the names of those not ready in the chat window." ) + "###List unready names in chat.", ref Configuration.mShowReadyCheckResultsInChat );

				if( Configuration.ShowReadyCheckResultsInChat )
				{
					ImGui.Spacing();

					ImGui.Indent();
					ImGui.Text( Loc.Localize( "Config Option: Max Names in Chat", "Maximum number of names to show in chat:" ) );
					ImGui.SliderInt( "##MaxUnreadyNamesToShowInChat", ref Configuration.mMaxUnreadyToListInChat, 1, 48 );
					ImGui.Spacing();
					ImGui.Text( Loc.Localize( "Config Option: Chat Message Channel", "Chat Log Channel:" ) );
					ImGuiHelpMarker( string.Format( Loc.Localize( "Help: Chat Message Channel", "Sets the channel in which this chat message is shown.  Leave this set to the default value ({0}) unless it causes problems with your chat configuration.  This only affects the unready players message; all other messages from this plugin respect your choice of chat channel in Dalamud settings." ), LocalizationHelpers.GetTranslatedChatChannelName( Dalamud.Game.Text.XivChatType.SystemMessage ) ) );
					if( ImGui.BeginCombo( "###NotReadyMessageChatChannelDropdown", LocalizationHelpers.GetTranslatedChatChannelName( Configuration.ChatChannelToUseForNotReadyMessage ) ) )
					{
						foreach( Dalamud.Game.Text.XivChatType entry in Enum.GetValues( typeof( Dalamud.Game.Text.XivChatType ) ) )
						{
							if( ImGui.Selectable( LocalizationHelpers.GetTranslatedChatChannelName( entry ) ) ) Configuration.ChatChannelToUseForNotReadyMessage = entry;
						}
						ImGui.EndCombo();
					}
					ImGui.Unindent();

					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
				}

				ImGui.Checkbox( Loc.Localize( "Config Option: Draw on Party Alliance Lists", "Draw ready check on party/alliance lists." ) + "###Draw ready check on party/alliance lists.", ref Configuration.mShowReadyCheckOnPartyAllianceList );

				if( Configuration.ShowReadyCheckOnPartyAllianceList )
				{
					ImGui.Spacing();

					ImGui.Indent();
					ImGui.Text( Loc.Localize( "Config Option: Clear Party Alliance List Settings", "Clear ready check from party/alliance lists:" ) );
					ImGui.Checkbox( Loc.Localize( "Config Option: Clear Party Alliance List upon Entering Combat", "Upon entering combat." ) + "###Upon entering combat.", ref Configuration.mClearReadyCheckOverlayInCombat );
					ImGui.Checkbox( Loc.Localize( "Config Option: Clear Party Alliance List upon Entering Instance", "Upon entering instance." ) + "###Upon entering instance.", ref Configuration.mClearReadyCheckOverlayEnteringInstance );
					ImGui.Checkbox( Loc.Localize( "Config Option: Clear Party Alliance List upon Enteringing Combat in Instance", "Upon entering combat while in instance." ) + "###Upon entering combat while in instance.", ref Configuration.mClearReadyCheckOverlayInCombatInInstancedCombat );
					ImGui.Checkbox( Loc.Localize( "Config Option: Clear Party Alliance List after X Seconds:", "After a certain number of seconds:" ) + "###After X seconds.", ref Configuration.mClearReadyCheckOverlayAfterTime );
					ImGuiHelpMarker( Loc.Localize( "Help: Clear Party Alliance List after X Seconds", "Changes to this setting will not take effect until the next ready check concludes." ) );
					ImGui.DragInt( "###TimeUntilClearOverlaySlider", ref Configuration.mTimeUntilClearReadyCheckOverlay_Sec, 1.0f, 30, 900, "%d", ImGuiSliderFlags.AlwaysClamp );
					ImGui.Spacing();
					ImGui.Text( Loc.Localize( "Config Section: Icon Size/Offset", "Party and Alliance List Icon Size/Offset:" ) );
					ImGui.DragFloat2( Loc.Localize( "Config Option: Party List Icon Offset", "Party List Icon Offset" ) + "###PartyListIconOffset", ref Configuration.mPartyListIconOffset, 1f, -100f, 100f );
					ImGui.DragFloat( Loc.Localize( "Config Option: Party List Icon Scale", "Party List Icon Scale" ) + "###PartyListIconScale", ref Configuration.mPartyListIconScale, 0.1f, 0.3f, 5.0f, "%f", ImGuiSliderFlags.AlwaysClamp );
					ImGui.DragFloat2( Loc.Localize( "Config Option: Alliance List Icon Offset", "Alliance List Icon Offset" ) + "###AllianceListIconOffset", ref Configuration.mAllianceListIconOffset, 1f, -100f, 100f );
					ImGui.DragFloat( Loc.Localize( "Config Option: Alliance List Icon Scale", "Alliance List Icon Scale" ) + "###AllianceListIconScale", ref Configuration.mAllianceListIconScale, 0.1f, 0.3f, 5.0f, "%f", ImGuiSliderFlags.AlwaysClamp );
					//ImGui.DragFloat2( Loc.Localize( "Config Option: Cross-World Alliance List Icon Offset", "Cross-World Alliance List Icon Offset" ) + "###CrossWorldAllianceListIconOffset", ref Configuration.mCrossWorldAllianceListIconOffset, 1f, -100f, 100f );
					//ImGui.DragFloat( Loc.Localize( "Config Option: Cross-World Alliance List Icon Scale", "Cross-World Alliance List Icon Scale" ) + "###CrossWorldAllianceListIconScale", ref Configuration.mCrossWorldAllianceListIconScale, 0.1f, 0.3f, 5.0f, "%d", ImGuiSliderFlags.AlwaysClamp );
					ImGui.Unindent();
				}

				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();

				/*if( ImGui.Button( Loc.Localize( "Button: Save", "Save" ) + "###Save Button" ) )
				{
					Configuration.Save();
				}
				ImGui.SameLine();*/
				if( ImGui.Button( Loc.Localize( "Button: Save and Close", "Save and Close" ) + "###Save and Close" ) )
				{
					Configuration.Save();
					SettingsWindowVisible = false;
				}
			}

			ImGui.End();
		}

		private void DrawReadyCheckResultsWindow()
		{
			if( !ReadyCheckResultsWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSizeConstraints( new Vector2( 180, 100 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Ready Check Results", "Latest Ready Check Results" ) + "###Latest Ready Check Results", ref mReadyCheckResultsWindowVisible,
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				var list = Plugin.GetProcessedReadyCheckData();
				if( list != null )
				{
					//	We have to sort and reorganize this yet again because of how ImGui tables work ;_;
					list.Sort( ( a, b ) => a.GroupIndex.CompareTo( b.GroupIndex ) );
					var tableList = new List<List<CorrelatedReadyCheckEntry>>();
					foreach( var player in list )
					{
						if( tableList.Count <= player.GroupIndex )
						{
							tableList.Add( new List<CorrelatedReadyCheckEntry>() );
						}
						tableList[player.GroupIndex].Add( player );
					}

					if( ImGui.BeginTable( "###LatestReadyCheckResultsTable", tableList.Count ) )
					{
						for( var i = 0; i < 8; ++i )
						{
							ImGui.TableNextRow();
							for( var j = 0; j < tableList.Count; ++j )
							{
								ImGui.TableSetColumnIndex( j );
								if( i < tableList[j].Count )
								{
									if( tableList[j][i].ReadyState == AgentReadyCheck.ReadyCheckStatus.Ready )
									{
										ImGui.Image( mReadyCheckIconTexture.ImGuiHandle, new Vector2( 24 ), new Vector2( 0.0f ), new Vector2( 0.5f, 1.0f ) );
									}
									else if( tableList[j][i].ReadyState == AgentReadyCheck.ReadyCheckStatus.NotReady )
									{
										ImGui.Image( mReadyCheckIconTexture.ImGuiHandle, new Vector2( 24 ), new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
									}
									else if( tableList[j][i].ReadyState == AgentReadyCheck.ReadyCheckStatus.MemberNotPresent )
									{
										ImGui.Image( mNotPresentIconTexture.ImGuiHandle, new Vector2( 24 ) );
									}
									else
									{
										ImGui.Image( mUnknownStatusIconTexture.ImGuiHandle, new Vector2( 24 ), new Vector2( 0.0f ), new Vector2( 1.0f ), new Vector4( 0.0f ) );
									}
									ImGui.SameLine();
									ImGui.Text( tableList[j][i].Name );
								}
								//	Probably don't need this, but tables are sometimes getting clobbered, so putting it here just in case that helps.
								else
								{
									ImGui.Text( " " );
								}
							}
						}
						ImGui.EndTable();
					}
				}
				else
				{
					ImGui.Text( Loc.Localize( "Placeholder: No Ready Check Results Exist", "No ready check has yet occurred.") );
				}

				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();

				if( ImGui.Button( Loc.Localize( "Button: Close", "Close" ) + "###Close" ) )
				{
					ReadyCheckResultsWindowVisible = false;
				}
			}

			ImGui.End();
		}

		protected void DrawDebugWindow()
		{
			if( !DebugWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 1340, 568 ) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 340 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Ready Check and Alliance Debug Data", "Ready Check and Alliance Debug Data" ) + "###Ready Check and Alliance Debug Data", ref mDebugWindowVisible ) )
			{
				ImGui.PushFont( UiBuilder.MonoFont );
				try
				{
					unsafe
					{
						var pAgentHUD = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentHUD();
						if( (nint)FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance() == nint.Zero )
						{
							ImGui.Text( "The GroupManager instance pointer is null!" );
						}
						else if( (nint)FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance() == nint.Zero )
						{
							ImGui.Text( "The InfoProxyCrossRealm instance pointer is null!" );
						}
						else
						{
							var readyCheckdata = AgentReadyCheck.Instance()->ReadyCheckEntriesSpan;

							ImGui.Columns( 5 );
							ImGui.Text( "General Info:" );

							ImGui.Text( $"Number of Party Members: {FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->MemberCount}" );
							ImGui.Text( $"Is Cross-World: {FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->IsCrossRealm}" );
							var crossWorldGroupCount = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.Instance()->GroupCount;
							ImGui.Text( $"Number of Cross-World Groups: {crossWorldGroupCount}" );
							for( var i = 0; i < crossWorldGroupCount; ++i )
							{
								ImGui.Text( $"Number of Party Members (Group {i}): {FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetGroupMemberCount( i )}" );
							}
							ImGui.Text( $"Ready check is active: {Plugin.ReadyCheckActive}" );
							ImGui.Spacing();
							ImGui.Spacing();
							ImGui.Spacing();
							ImGui.Text( $"Ready Check Object Address: 0x{MemoryHandler.DEBUG_GetReadyCheckObjectAddress():X}" );
							ImGui.Text( $"Hud Agent Address: 0x{new nint(pAgentHUD):X}" );
							ImGui.Checkbox( "Show Raw Readycheck Data", ref mDebugRawWindowVisible );
							ImGui.Checkbox( "Show Processed Readycheck Data", ref mDebugProcessedWindowVisible );
							ImGui.Checkbox( "Debug Drawing on Party List", ref mDEBUG_DrawPlaceholderData );
							ImGui.Checkbox( "Allow Cross-world Alliance List Drawing", ref mDEBUG_AllowCrossWorldAllianceDrawing );
							{
								if( ImGui.Button( "Test Chat Message" ) )
								{
									Plugin.ListUnreadyPlayersInChat( new List<string>( LocalizationHelpers.TestNames.Take( mDEBUG_NumNamesToTestChatMessage ) ) );
								}
								ImGui.SliderInt( "Number of Test Names", ref mDEBUG_NumNamesToTestChatMessage, 1, LocalizationHelpers.TestNames.Length );
							}
							if( ImGui.Button( "Export Localizable Strings" ) )
							{
								var pwd = Directory.GetCurrentDirectory();
								Directory.SetCurrentDirectory( Plugin.PluginInterface.AssemblyLocation.DirectoryName! );
								Loc.ExportLocalizable();
								Directory.SetCurrentDirectory( pwd );
							}
							ImGui.Spacing();
							ImGui.Spacing();
							ImGui.Spacing();
							ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
							ImGui.Text( "Ready Check Object Address:" );
							ImGuiHelpMarker( Loc.Localize( "Help: Debug Set Object Address Warning", "DO NOT TOUCH THIS UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING AND WHY; THE ABSOLUTE BEST CASE IS A PLUGIN CRASH." ) );
							ImGui.InputText( "##ObjectAddressSetInputBox", ref mDEBUG_ReadyCheckObjectAddressInputString, 16 );
							if( ImGui.Button( "Set Ready Check Object Address" ) )
							{
								var isValidPointer = nint.TryParse( mDEBUG_ReadyCheckObjectAddressInputString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var ptr );
								if( isValidPointer ) MemoryHandler.DEBUG_SetReadyCheckObjectAddress( ptr );
							}
							ImGui.PopStyleColor();
							ImGui.NextColumn();
							ImGui.Text( "Ready Check Data:" );
							for( var i = 0; i < readyCheckdata.Length; ++i )
							{
								ImGui.Text( $"ID: {readyCheckdata[i].ContentID:X16}, State: {readyCheckdata[i].Status}" );
							}
							ImGui.NextColumn();
							ImGui.Text( "Party Data:" );
							for( var i = 0; i < 8; ++i )
							{
								var pGroupMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetPartyMemberByIndex( i );
								if( (nint)pGroupMember != nint.Zero )
								{
									var name = MemoryHelper.ReadSeStringNullTerminated( (nint)pGroupMember->Name ).ToString();
									string classJobAbbr = JobDict.TryGetValue( pGroupMember->ClassJob, out classJobAbbr ) ? classJobAbbr : "ERR";
									ImGui.Text( $"Job: {classJobAbbr}, OID: {pGroupMember->ObjectID:X8}, CID: {pGroupMember->ContentID:X16}, Name: {name}" );
								}
								else
								{
									ImGui.Text( "Party member returned as null pointer." );
								}
							}
							for( var i = 0; i < 16; ++i )
							{
								var pGroupMember = FFXIVClientStructs.FFXIV.Client.Game.Group.GroupManager.Instance()->GetAllianceMemberByIndex( i );
								if( (nint)pGroupMember != nint.Zero )
								{
									var name = MemoryHelper.ReadSeStringNullTerminated( (nint)pGroupMember->Name ).ToString();
									string classJobAbbr = JobDict.TryGetValue( pGroupMember->ClassJob, out classJobAbbr ) ? classJobAbbr : "ERR";
									ImGui.Text( $"Job: {classJobAbbr}, OID: {pGroupMember->ObjectID:X8}, CID: {pGroupMember->ContentID:X16}, Name: {name}" );
								}
								else
								{
									ImGui.Text( "Alliance member returned as null pointer." );
								}
							}
							ImGui.NextColumn();
							ImGui.Text( "Cross-World Party Data:" );
							for( var i = 0; i < crossWorldGroupCount; ++i )
							{
								for( var j = 0; j < FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetGroupMemberCount( i ); ++j )
								{
									var pGroupMember = FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCrossRealm.GetGroupMember( (uint)j, i );
									if( (nint)pGroupMember != nint.Zero )
									{
										var name = MemoryHelper.ReadSeStringNullTerminated( (nint)pGroupMember->Name ).ToString();
										ImGui.Text( $"Group: {pGroupMember->GroupIndex}, OID: {pGroupMember->ObjectId:X8}, CID: {pGroupMember->ContentId:X16}, Name: {name}" );
									}
								}
							}
							ImGui.NextColumn();
							ImGui.Text( $"AgentHUD Group Size: {pAgentHUD->RaidGroupSize}" );
							ImGui.Text( $"AgentHUD Party Size: {pAgentHUD->PartyMemberCount}" );
							ImGui.Text( "AgentHUD Party Members:" );
							for( var i = 0; i < 8; ++i )
							{
								var partyMemberData = pAgentHUD->PartyMemberListSpan[i];
								ImGui.Text( $"Object Address: 0x{(nint) partyMemberData.Object:X}\r\nName Address: 0x{(nint) partyMemberData.Name:X}\r\nName: {MemoryHelper.ReadSeStringNullTerminated((nint) partyMemberData.Name)}\r\nCID: {partyMemberData.ContentId:X}\r\nOID: {partyMemberData.ObjectId:X}" );
							}
							ImGui.Text( "AgentHUD Raid Members:" );
							for( var i = 0; i < 40; ++i )
							{
								ImGui.Text( $"{i:D2}: {pAgentHUD->RaidMemberIds[i]:X8}" );
							}
							ImGui.Columns();
						}
					}
				}
				finally
				{
					ImGui.PopFont();
				}
			}

			//	We're done.
			ImGui.End();
		}

		protected void DrawDebugProcessedWindow()
		{
			if( !DebugProcessedWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 1340, 568 ) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 340 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Processed Ready Check Data", "Debug: Processed Ready Check Data" ) + "###Processed Ready Check Data", ref mDebugProcessedWindowVisible ) )
			{
				ImGui.PushFont( UiBuilder.MonoFont );
				try
				{
					var list = Plugin.GetProcessedReadyCheckData();
					if( list != null )
					{
						foreach( var player in list )
						{
							ImGui.Text( $"OID: {player.ObjectID:X8}, CID: {player.ContentID:X16}, Group: {player.GroupIndex}, Index: {player.MemberIndex}, State: {(byte)player.ReadyState}, Name: {player.Name}" );
						}
					}

					if( ImGui.Button( Loc.Localize( "Button: Close", "Close" ) + "###Close" ) )
					{
						DebugProcessedWindowVisible = false;
					}
				}
				finally
				{
					ImGui.PopFont();
				}
			}

			//	We're done.
			ImGui.End();
		}

		unsafe protected void DrawOnPartyAllianceLists()
		{
			if( ( mDEBUG_DrawPlaceholderData || ( Configuration.ShowReadyCheckOnPartyAllianceList && ReadyCheckValid ) ) && Plugin.GameGui != null )
			{
				const ImGuiWindowFlags flags =	ImGuiWindowFlags.NoDecoration |
												ImGuiWindowFlags.NoSavedSettings |
												ImGuiWindowFlags.NoMove |
												ImGuiWindowFlags.NoMouseInputs |
												ImGuiWindowFlags.NoFocusOnAppearing |
												ImGuiWindowFlags.NoBackground |
												ImGuiWindowFlags.NoNav;

				ImGuiHelpers.ForceNextWindowMainViewport();
				ImGui.SetNextWindowPos( ImGui.GetMainViewport().Pos );
				ImGui.SetNextWindowSize( ImGui.GetMainViewport().Size );
				if( ImGui.Begin( "##ReadyCheckOverlayWindow", flags ) )
				{
					var pPartyList = (AtkUnitBase*) Plugin.GameGui.GetAddonByName( "_PartyList", 1 );
					var pAlliance1List = (AtkUnitBase*) Plugin.GameGui.GetAddonByName( "_AllianceList1", 1 );
					var pAlliance2List = (AtkUnitBase*) Plugin.GameGui.GetAddonByName( "_AllianceList2", 1 );
					var pCrossWorldAllianceList = (AtkUnitBase*) Plugin.GameGui.GetAddonByName( "Alliance48", 1 );

					if( mDEBUG_DrawPlaceholderData )
					{
						if( (nint)pPartyList != nint.Zero && pPartyList->IsVisible )
						{
							for( var i = 0; i < 8; ++i )
							{
								DrawOnPartyList( i, AgentReadyCheck.ReadyCheckStatus.Ready, pPartyList, ImGui.GetWindowDrawList() );
							}
						}

						if( (nint)pAlliance1List != nint.Zero && pAlliance1List->IsVisible )
						{
							for( var i = 0; i < 8; ++i )
							{
								DrawOnAllianceList( i, AgentReadyCheck.ReadyCheckStatus.Ready, pAlliance1List, ImGui.GetWindowDrawList() );
							}
						}

						if( (nint)pAlliance2List != nint.Zero && pAlliance2List->IsVisible )
						{
							for( var i = 0; i < 8; ++i )
							{
								DrawOnAllianceList( i, AgentReadyCheck.ReadyCheckStatus.Ready, pAlliance2List, ImGui.GetWindowDrawList() );
							}
						}

						if( (nint)pCrossWorldAllianceList != nint.Zero && pCrossWorldAllianceList->IsVisible )
						{
							for( var j = 1; j < 6; ++j )
							{
								for( var i = 0; i < 8; ++i )
								{
									DrawOnCrossWorldAllianceList( j, i, AgentReadyCheck.ReadyCheckStatus.Ready, pCrossWorldAllianceList, ImGui.GetWindowDrawList() );
								}
							}
						}
					}
					else
					{
						var data = Plugin.GetProcessedReadyCheckData();
						if( data != null )
						{
							foreach( var result in data )
							{
								var indices = MemoryHandler.GetHUDIndicesForChar( result.ContentID, result.ObjectID );
								if( indices == null ) continue;
								switch( indices.Value.GroupNumber )
								{
									case 0:
										DrawOnPartyList( indices.Value.PartyMemberIndex, result.ReadyState, pPartyList, ImGui.GetWindowDrawList() );
										break;
									case 1:
										if( indices.Value.CrossWorld ) break;	//***** TODO: Do something when crossworld alliances are fixed.
										else DrawOnAllianceList( indices.Value.PartyMemberIndex, result.ReadyState, pAlliance1List, ImGui.GetWindowDrawList() );
										break;
									case 2:
										if( indices.Value.CrossWorld ) break;   //***** TODO: Do something when crossworld alliances are fixed.
										else DrawOnAllianceList( indices.Value.PartyMemberIndex, result.ReadyState, pAlliance2List, ImGui.GetWindowDrawList() );
										break;
									default:
										if( indices.Value.CrossWorld ) break;   //***** TODO: Do something when crossworld alliances are fixed.
										break;
								}
							}
						}
					}
				}

				ImGui.End();
			}
		}

		unsafe protected void DrawOnPartyList( int listIndex, AgentReadyCheck.ReadyCheckStatus readyCheckState, AtkUnitBase* pPartyList, ImDrawListPtr drawList )
		{
			if( listIndex < 0 || listIndex > 7 ) return;
			var partyMemberNodeIndex = 22 - listIndex;
			var iconNodeIndex = 4;
			var partyAlign = pPartyList->UldManager.NodeList[3]->Y;

			var pPartyMemberNode = pPartyList->UldManager.NodeListSize > partyMemberNodeIndex ? (AtkComponentNode*) pPartyList->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) nint.Zero;
			if( (nint)pPartyMemberNode != nint.Zero )
			{
				var pIconNode = pPartyMemberNode->Component->UldManager.NodeListSize > iconNodeIndex ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) nint.Zero;
				if( (nint)pIconNode != nint.Zero )
				{
					//	Note: sub-nodes don't scale, so we have to account for the addon's scale.
					var iconOffset = ( new Vector2( -7, -5 ) + Configuration.PartyListIconOffset ) * pPartyList->Scale;
					var iconSize = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 ) * Configuration.PartyListIconScale * pPartyList->Scale;
					var iconPos = new Vector2(	pPartyList->X + pPartyMemberNode->AtkResNode.X * pPartyList->Scale + pIconNode->X * pPartyList->Scale + pIconNode->Width * pPartyList->Scale / 2,
													pPartyList->Y + partyAlign + pPartyMemberNode->AtkResNode.Y * pPartyList->Scale + pIconNode->Y * pPartyList->Scale + pIconNode->Height * pPartyList->Scale / 2 );
					iconPos += iconOffset;

					if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.NotReady )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
					}
					else if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.Ready )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f, 0.0f ), new Vector2( 0.5f, 1.0f ) );
					}
					else if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.MemberNotPresent )
					{
						drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
					}
				}
			}
		}

		unsafe protected void DrawOnAllianceList( int listIndex, AgentReadyCheck.ReadyCheckStatus readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList )
		{
			if( listIndex < 0 || listIndex > 7 ) return;
			var partyMemberNodeIndex = 9 - listIndex;
			var iconNodeIndex = 5;

			var pAllianceMemberNode = pAllianceList->UldManager.NodeListSize > partyMemberNodeIndex ? (AtkComponentNode*) pAllianceList->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) nint.Zero;
			if( (nint)pAllianceMemberNode != nint.Zero )
			{
				var pIconNode = pAllianceMemberNode->Component->UldManager.NodeListSize > iconNodeIndex ? pAllianceMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) nint.Zero;
				if( (nint)pIconNode != nint.Zero )
				{
					var iconOffset = ( new Vector2( 0, 0 ) + Configuration.AllianceListIconOffset ) * pAllianceList->Scale;
					var iconSize = new Vector2( pIconNode->Width / 3, pIconNode->Height / 3 ) * Configuration.AllianceListIconScale * pAllianceList->Scale;
					var iconPos = new Vector2(	pAllianceList->X + pAllianceMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2,
													pAllianceList->Y + pAllianceMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2 );
					iconPos += iconOffset;

					if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.NotReady )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
					}
					else if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.Ready )
					{
						drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f ), new Vector2( 0.5f, 1.0f ) );
					}
					else if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.MemberNotPresent )
					{
						drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
					}
				}
			}
		}

		unsafe protected void DrawOnCrossWorldAllianceList( int allianceIndex, int partyMemberIndex, AgentReadyCheck.ReadyCheckStatus readyCheckState, AtkUnitBase* pAllianceList, ImDrawListPtr drawList )
		{
			if( allianceIndex < 1 || allianceIndex > 5 ) return;
			if( partyMemberIndex < 0 || partyMemberIndex > 7 ) return;
			var allianceNodeIndex = 8 - allianceIndex;
			var partyMemberNodeIndex = 8 - partyMemberIndex;
			var iconNodeIndex = 2;

			//***** TODO: This *occasionally* crashes, and I don't understand why.  Best guess is that the node list is not populated all at once, but grows as the addon is created.*****
			var pAllianceNode = pAllianceList->UldManager.NodeListSize > allianceNodeIndex ? (AtkComponentNode*) pAllianceList->UldManager.NodeList[allianceNodeIndex] : (AtkComponentNode*) nint.Zero;
			if( (nint)pAllianceNode != nint.Zero )
			{
				var pPartyMemberNode = pAllianceNode->Component->UldManager.NodeListSize > partyMemberNodeIndex ? (AtkComponentNode*) pAllianceNode->Component->UldManager.NodeList[partyMemberNodeIndex] : (AtkComponentNode*) nint.Zero;
				if( (nint)pPartyMemberNode != nint.Zero )
				{
					var pIconNode = pPartyMemberNode->Component->UldManager.NodeListSize > iconNodeIndex ? pPartyMemberNode->Component->UldManager.NodeList[iconNodeIndex] : (AtkResNode*) nint.Zero;
					if( (nint)pIconNode != nint.Zero )
					{
						var iconOffset = ( new Vector2( 0, 0 ) + Configuration.CrossWorldAllianceListIconOffset ) * pAllianceList->Scale;
						var iconSize = new Vector2( pIconNode->Width / 2, pIconNode->Height / 2 ) * Configuration.CrossWorldAllianceListIconScale * pAllianceList->Scale;
						var iconPos = new Vector2(	pAllianceList->X + pAllianceNode->AtkResNode.X * pAllianceList->Scale + pPartyMemberNode->AtkResNode.X * pAllianceList->Scale + pIconNode->X * pAllianceList->Scale + pIconNode->Width * pAllianceList->Scale / 2,
														pAllianceList->Y + pAllianceNode->AtkResNode.Y * pAllianceList->Scale + pPartyMemberNode->AtkResNode.Y * pAllianceList->Scale + pIconNode->Y * pAllianceList->Scale + pIconNode->Height * pAllianceList->Scale / 2 );
						iconPos += iconOffset;

						if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.NotReady )
						{
							drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.5f, 0.0f ), new Vector2( 1.0f ) );
						}
						else if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.Ready )
						{
							drawList.AddImage( mReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2( 0.0f, 0.0f ), new Vector2( 0.5f, 1.0f ) );
						}
						else if( readyCheckState == AgentReadyCheck.ReadyCheckStatus.MemberNotPresent )
						{
							drawList.AddImage( mNotPresentIconTexture.ImGuiHandle, iconPos, iconPos + iconSize );
						}
					}
				}
			}
		}

		protected void ImGuiHelpMarker( string description, bool sameLine = true, string marker = "(?)" )
		{
			if( sameLine ) ImGui.SameLine();
			ImGui.TextDisabled( marker );
			if( ImGui.IsItemHovered() )
			{
				ImGui.BeginTooltip();
				ImGui.PushTextWrapPos( ImGui.GetFontSize() * 35.0f );
				ImGui.TextUnformatted( description );
				ImGui.PopTextWrapPos();
				ImGui.EndTooltip();
			}
		}

		public void ShowReadyCheckOverlay()
		{
			ReadyCheckValid = true;
		}

		public void InvalidateReadyCheck()
		{
			ReadyCheckValid = false;
		}

		protected Dictionary<uint, string> JobDict { get; set; } = new Dictionary<uint, string>();

		protected IDalamudTextureWrap mReadyCheckIconTexture = null;
		protected IDalamudTextureWrap mUnknownStatusIconTexture = null;
		protected IDalamudTextureWrap mNotPresentIconTexture = null;

		protected bool ReadyCheckValid { get; set; }
		protected bool mDEBUG_DrawPlaceholderData = false;
		protected string mDEBUG_ReadyCheckObjectAddressInputString = "";
		protected bool mDEBUG_AllowCrossWorldAllianceDrawing = false;
		protected int mDEBUG_NumNamesToTestChatMessage = 5;

		//	Need a real backing field on the following properties for use with ImGui.
		protected bool mSettingsWindowVisible = false;
		public bool SettingsWindowVisible
		{
			get { return mSettingsWindowVisible; }
			set { mSettingsWindowVisible = value; }
		}

		protected bool mReadyCheckResultsWindowVisible = false;
		public bool ReadyCheckResultsWindowVisible
		{
			get { return mReadyCheckResultsWindowVisible; }
			set { mReadyCheckResultsWindowVisible = value; }
		}

		protected bool mDebugWindowVisible = false;
		public bool DebugWindowVisible
		{
			get { return mDebugWindowVisible; }
			set { mDebugWindowVisible = value; }
		}

		protected bool mDebugRawWindowVisible = false;
		public bool DebugRawWindowVisible
		{
			get { return mDebugRawWindowVisible; }
			set { mDebugRawWindowVisible = value; }
		}

		protected bool mDebugProcessedWindowVisible = false;
		public bool DebugProcessedWindowVisible
		{
			get { return mDebugProcessedWindowVisible; }
			set { mDebugProcessedWindowVisible = value; }
		}
	}
}