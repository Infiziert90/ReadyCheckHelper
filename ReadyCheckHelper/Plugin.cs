using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CheapLoc;
using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;

namespace ReadyCheckHelper
{
	public sealed class Plugin : IDalamudPlugin
	{
		[PluginService] public static IChatGui Chat { get; private set; } = null!;
		[PluginService] public static ICondition Condition { get; private set; } = null!;
		[PluginService] public static IFramework Framework { get; private set; } = null!;
		[PluginService] public static IClientState ClientState { get; private set; } = null!;
		[PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
		[PluginService] public static IGameGui GameGui { get; private set; } = null!;
		[PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
		[PluginService] public static IDataManager DataManager { get; private set; } = null!;
		[PluginService] public static ITextureProvider Texture { get; private set; } = null!;
		[PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
		[PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
		[PluginService] public static IPluginLog Log { get; private set; } = null!;

		private const string TextCommandName = "/pready";
		private readonly DalamudLinkPayload OpenReadyCheckWindowLink;

		private Configuration Configuration { get; init; }
		private PluginUI PluginUi { get; init; }

		private List<UInt32> mInstancedTerritories = new();
		private List<CorrelatedReadyCheckEntry> mProcessedReadyCheckData;
		private Object mProcessedReadyCheckDataLockObj = new();
		private CancellationTokenSource mTimedOverlayCancellationSource;
		public bool ReadyCheckActive { get; private set; }

		//	Initialization
		public Plugin()
		{
			//	Configuration
			Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
			Configuration.Initialize( PluginInterface );
			MemoryHandler.Init();

			//	Localization and Command Initialization
			OnLanguageChanged( PluginInterface.UiLanguage );
			OpenReadyCheckWindowLink = PluginInterface.AddChatLinkHandler( 1001, ( i, m ) =>
			{
				ShowBestAvailableReadyCheckWindow();
			} );
			LocalizationHelpers.Init();

			//	UI Initialization
			PluginUi = new PluginUI(this, Configuration);
			PluginInterface.UiBuilder.Draw += DrawUI;
			PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
			PluginUi.Initialize();

			//	Misc.
			PopulateInstancedTerritoriesList();

			//	Event Subscription
			PluginInterface.LanguageChanged += OnLanguageChanged;
			Condition.ConditionChange += OnConditionChanged;
			ClientState.TerritoryChanged += OnTerritoryChanged;
			ClientState.Logout += OnLogout;
			Framework.Update += OnGameFrameworkUpdate;
			MemoryHandler.ReadyCheckInitiatedEvent += OnReadyCheckInitiated;
			MemoryHandler.ReadyCheckCompleteEvent += OnReadyCheckCompleted;
		}

		//	Cleanup
		public void Dispose()
		{
			MemoryHandler.ReadyCheckInitiatedEvent -= OnReadyCheckInitiated;
			MemoryHandler.ReadyCheckCompleteEvent -= OnReadyCheckCompleted;
			Framework.Update -= OnGameFrameworkUpdate;
			ClientState.Logout -= OnLogout;
			ClientState.TerritoryChanged -= OnTerritoryChanged;
			Condition.ConditionChange -= OnConditionChanged;
			MemoryHandler.Uninit();
			PluginInterface.UiBuilder.Draw -= DrawUI;
			PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
			PluginInterface.LanguageChanged -= OnLanguageChanged;
			PluginInterface.RemoveChatLinkHandler();
			CommandManager.RemoveHandler( TextCommandName );
			PluginUi?.Dispose();
			mInstancedTerritories.Clear();
			LocalizationHelpers.Uninit();
			mTimedOverlayCancellationSource?.Dispose();
			mTimedOverlayCancellationSource = null;
		}

		private void OnLanguageChanged( string langCode )
		{
			var allowedLang = new List<string>{ "es", "fr", "ja" };

			Log.Information( "Trying to set up Loc for culture {0}", langCode );

			if( allowedLang.Contains( langCode ) )
			{
				Loc.Setup( File.ReadAllText( Path.Join( Path.Join( PluginInterface.AssemblyLocation.DirectoryName, "Resources\\Localization\\" ), $"loc_{langCode}.json" ) ) );
			}
			else
			{
				Loc.SetupWithFallbacks();
			}

			//	Set up the command handler with the current language.
			if( CommandManager.Commands.ContainsKey( TextCommandName ) )
			{
				CommandManager.RemoveHandler( TextCommandName );
			}
			CommandManager.AddHandler( TextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = String.Format( Loc.Localize( "Plugin Text Command Description", "Use {0} to open the the configuration window." ), "\"/pready config\"" )
			} );
		}

		//	Text Commands
		private void ProcessTextCommand( string command, string args )
		{
			//*****TODO: Don't split, just substring off of the first space so that other stuff is preserved verbatim.
			//	Seperate into sub-command and paramters.
			string subCommand = "";
			string subCommandArgs = "";
			string[] argsArray = args.Split( ' ' );
			if( argsArray.Length > 0 )
			{
				subCommand = argsArray[0];
			}
			if( argsArray.Length > 1 )
			{
				//	Recombine because there might be spaces in JSON or something, that would make splitting it bad.
				for( int i = 1; i < argsArray.Length; ++i )
				{
					subCommandArgs += argsArray[i] + ' ';
				}
				subCommandArgs = subCommandArgs.Trim();
			}

			//	Process the commands.
			bool suppressResponse = Configuration.SuppressCommandLineResponses;
			string commandResponse = "";
			if( subCommand.Length == 0 )
			{
				//	For now just have no subcommands act like the config subcommand
				PluginUi.SettingsWindowVisible = !PluginUi.SettingsWindowVisible;
			}
			else if( subCommand.ToLower() == "config" )
			{
				PluginUi.SettingsWindowVisible = !PluginUi.SettingsWindowVisible;
			}
			else if( subCommand.ToLower() == "debug" )
			{
				PluginUi.DebugWindowVisible = !PluginUi.DebugWindowVisible;
			}
			else if( subCommand.ToLower() == "results" )
			{
				PluginUi.ReadyCheckResultsWindowVisible = !PluginUi.ReadyCheckResultsWindowVisible;
			}
			else if( subCommand.ToLower() == "clear" )
			{
				PluginUi.InvalidateReadyCheck();
			}
			else if( subCommand.ToLower() == "help" || subCommand.ToLower() == "?" )
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
				suppressResponse = false;
			}
			else
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
			}

			//	Send any feedback to the user.
			if( commandResponse.Length > 0 && !suppressResponse )
			{
				Chat.Print( commandResponse );
			}
		}

		private string ProcessTextCommand_Help( string args )
		{
			return args.ToLower() switch
			{
				"config" => Loc.Localize("Config Subcommand Help Message", "Opens the settings window."),
				"results" => Loc.Localize("Results Subcommand Help Message", "Opens a window containing the results of the last ready check to occur."),
				"clear" => Loc.Localize("Clear Subcommand Help Message", "Removes the most recent ready check icons from the party/alliance lists."),
				"debug" => Loc.Localize("Debug Subcommand Help Message", "Opens a debugging window containing party and ready check object data."),
				_ => string.Format(Loc.Localize("Basic Help Message", "This plugin works automatically; however, some text commands are supported.  Valid subcommands are {0}, {1}, and {2}.  Use \"{3} <subcommand>\" for more information on each subcommand."), "\"config\"", "\"results\"", "\"clear\"", "/pready help")
			};
		}

		private void DrawUI()
		{
			PluginUi.Draw();
		}

		private void DrawConfigUI()
		{
			PluginUi.SettingsWindowVisible = true;
		}

		private void OnGameFrameworkUpdate( IFramework framework )
		{
			if( ClientState.IsLoggedIn && ReadyCheckActive )
				ProcessReadyCheckResults();
		}

		private void OnReadyCheckInitiated( object sender, EventArgs e )
		{
			//	Shouldn't really be getting here if someone is logged out, but better safe than sorry.
			if( !ClientState.IsLoggedIn )
				return;

			//	Flag that we should start processing the data every frame.
			ReadyCheckActive = true;
			PluginUi.ShowReadyCheckOverlay();
			mTimedOverlayCancellationSource?.Cancel();
		}

		private void OnReadyCheckCompleted( object sender, EventArgs e )
		{
			//	Shouldn't really be getting here if someone is logged out, but better safe than sorry.
			if( !ClientState.IsLoggedIn )
				return;

			//	Flag that we don't need to keep updating.
			ReadyCheckActive = false;
			PluginUi.ShowReadyCheckOverlay();

			//	Process the data one last time to ensure that we have the latest results.
			ProcessReadyCheckResults();

			//	Construct a list of who's not ready.
			var notReadyList = new List<string>();

			lock( mProcessedReadyCheckDataLockObj )
			{
				foreach( var person in mProcessedReadyCheckData )
				{
					if( person.ReadyState == MemoryHandler.ReadyCheckStateEnum.NotReady ||
						person.ReadyState == MemoryHandler.ReadyCheckStateEnum.CrossWorldMemberNotPresent )
					{
						notReadyList.Add( person.Name );
					}
				}
			}

			//	Print it to chat in the desired format.
			if( Configuration.ShowReadyCheckResultsInChat )
			{
				ListUnreadyPlayersInChat( notReadyList );
			}

			//	Start a task to clean up the icons on the party chat after the configured amount of time.
			if( Configuration.ClearReadyCheckOverlayAfterTime )
			{
				mTimedOverlayCancellationSource = new CancellationTokenSource();
				Task.Run(async () =>
				{
					var delaySec = Math.Max( 0, Math.Min( Configuration.TimeUntilClearReadyCheckOverlay_Sec, 900 ) ); //	Just to be safe...

					try
					{
						await Task.Delay( delaySec * 1000, mTimedOverlayCancellationSource.Token );
					}
					catch( OperationCanceledException )
					{
						return;
					}
					finally
					{
						mTimedOverlayCancellationSource?.Dispose();
						mTimedOverlayCancellationSource = null;
					}

					if( !ReadyCheckActive )
						PluginUi.InvalidateReadyCheck();
				} );
			}
		}

		private unsafe void ProcessReadyCheckResults()
		{
			if( (IntPtr)InfoProxyCrossRealm.Instance() != IntPtr.Zero )
			{
				if( (IntPtr)GroupManager.Instance() != IntPtr.Zero )
				{
					//	We're only in a crossworld party if the cross realm proxy says we are; however, it can say we're cross-realm when
					//	we're in a regular party if we entered an instance as a cross-world party, so account for that too.
					if( InfoProxyCrossRealm.Instance()->IsCrossRealm > 0 &&
					GroupManager.Instance()->MemberCount < 1 )
					{
						ProcessReadyCheckResults_CrossWorld();
					}
					else
					{
						ProcessReadyCheckResults_Regular();
					}
				}
			}
		}

		private unsafe void ProcessReadyCheckResults_Regular()
		{
			if( (IntPtr)GroupManager.Instance() != IntPtr.Zero )
			{
				try
				{
					var readyCheckData = MemoryHandler.GetReadyCheckInfo();
					var readyCheckProcessedList = new List<CorrelatedReadyCheckEntry>();
					bool foundSelf = false;

					//	Grab all of the alliance members here to make lookups easier since there's no function in client structs to get an alliance member by object ID.
					var allianceMemberDict = new Dictionary<UInt32, Tuple<UInt64, string, byte, byte>>();
					for( int j = 0; j < 2; ++j )
					{
						for( int i = 0; i < 8; ++i )
						{
							var pGroupMember = GroupManager.Instance()->GetAllianceMemberByGroupAndIndex( j, i );
							if( (IntPtr)pGroupMember != IntPtr.Zero )
							{
								string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pGroupMember->Name ).ToString();
								allianceMemberDict.TryAdd( pGroupMember->ObjectID, Tuple.Create( (UInt64)pGroupMember->ContentID, name, (byte)( j + 1 ), (byte)i ) );
							}
						}
					}

					//	Correlate all of the ready check entries with party/alliance members.
					for( int i = 0; i < readyCheckData.Length; ++i )
					{
						//	For our party, we need to do the correlation based on party data.
						if( i < GroupManager.Instance()->MemberCount )
						{
							//	For your immediate, local party, ready check data seems to be correlated with the party index, but with you always first in the list (anyone with an index below yours will be offset by one).
							var pFoundPartyMember = GroupManager.Instance()->GetPartyMemberByIndex( i );
							if( (IntPtr)pFoundPartyMember != IntPtr.Zero )
							{
								string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pFoundPartyMember->Name ).ToString();

								//	If it's us, we need to use the first entry in the ready check data.
								if( pFoundPartyMember->ObjectID == ClientState.LocalPlayer?.ObjectId )
								{
									readyCheckProcessedList.Insert( 0, new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[0].ReadyFlag, 0, 0 ) );
									foundSelf = true;
								}
								//	If it's before we've found ourselves, look ahead by one in the ready check data.
								else if( !foundSelf )
								{
									readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[i + 1].ReadyFlag, 0, (byte)( i + 1 ) ) );
								}
								//	Otherwise, use the same index in the ready check data.
								else
								{
									readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( name, (UInt64)pFoundPartyMember->ContentID, pFoundPartyMember->ObjectID, readyCheckData[i].ReadyFlag, 0, (byte)i ) );
								}
							}
						}
						//	For the alliance members, there should be object IDs to make matching easy.
						else if( readyCheckData[i].ID > 0 && ( readyCheckData[i].ID & 0xFFFFFFFF ) != 0xE0000000 )
						{
							Tuple<UInt64, string, byte, byte> temp = null;
							if( allianceMemberDict.TryGetValue( (uint)readyCheckData[i].ID, out temp ) )
							{
								readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( temp.Item2, temp.Item1, (UInt32)readyCheckData[i].ID, readyCheckData[i].ReadyFlag, temp.Item3, temp.Item4 ) );
							}
						}
						//***** TODO: How do things work if you're a non-cross-world alliance without people in the same zone? *****
						//This isn't possible through PF; is it still possible in the open world?
					}

					//	Assign to the persistent list if we've gotten through this without any problems.
					lock( mProcessedReadyCheckDataLockObj )
					{
						mProcessedReadyCheckData = readyCheckProcessedList;
					}
				}
				catch( Exception e )
				{
					Log.Debug( $"Exception caught in \"ProcessReadyCheckResults_Regular()\": {e}." );
				}
			}
		}

		private unsafe void ProcessReadyCheckResults_CrossWorld()
		{
			if( (IntPtr)InfoProxyCrossRealm.Instance() != IntPtr.Zero )
			{
				try
				{
					var readyCheckData = MemoryHandler.GetReadyCheckInfo();
					var readyCheckProcessedList = new List<CorrelatedReadyCheckEntry>();

					foreach( var readyCheckEntry in readyCheckData )
					{
						var pFoundPartyMember = InfoProxyCrossRealm.GetMemberByContentId( readyCheckEntry.ID );
						if( (IntPtr)pFoundPartyMember != IntPtr.Zero )
						{
							string name = MemoryHelper.ReadSeStringNullTerminated( (IntPtr)pFoundPartyMember->Name ).ToString();
							readyCheckProcessedList.Add( new CorrelatedReadyCheckEntry( name, pFoundPartyMember->ContentId, pFoundPartyMember->ObjectId, readyCheckEntry.ReadyFlag, pFoundPartyMember->GroupIndex, pFoundPartyMember->MemberIndex ) );
						}
					}

					//	Assign to the persistent list if we've gotten through this without any problems.
					lock( mProcessedReadyCheckDataLockObj )
					{
						mProcessedReadyCheckData = readyCheckProcessedList;
					}
				}
				catch( Exception e )
				{
					Log.Debug( $"Exception caught in \"ProcessReadyCheckResults_CrossWorld()\": {e}." );
				}
			}
		}

		public void ListUnreadyPlayersInChat( List<String> notReadyList )
		{
			if( notReadyList.Count > 0 )
			{
				//	Getting this from separate functions instead of just a localized string, since list construction may follow different rules in different languages.
				string notReadyString = "";
				switch( ClientState.ClientLanguage )
				{
					case ClientLanguage.Japanese:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_ja( notReadyList, Configuration.MaxUnreadyToListInChat );
						break;
					case ClientLanguage.English:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_en( notReadyList, Configuration.MaxUnreadyToListInChat );
						break;
					case ClientLanguage.German:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_de( notReadyList, Configuration.MaxUnreadyToListInChat );
						break;
					case ClientLanguage.French:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_fr( notReadyList, Configuration.MaxUnreadyToListInChat );
						break;
					/*case Dalamud.ClientLanguage.Korean:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_ko( notReadyList, Configuration.MaxUnreadyToListInChat );
						break;
					case Dalamud.ClientLanguage.Chinese:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_zh( notReadyList, Configuration.MaxUnreadyToListInChat );
						break;*/
					default:
						notReadyString = LocalizationHelpers.ConstructNotReadyString_en( notReadyList, Configuration.MaxUnreadyToListInChat );
						break;
				}

				//	If we don't delay the actual printing to chat, sometimes it comes out before the system message in the chat log.  I don't understand why it's an issue, but this is an easy kludge to make it work right consistently.
				Task.Run( async () =>
				{
					await Task.Delay( 500 );    //***** TODO: Make this value configurable, or fix the underlying issue. *****
					var chatEntry = new XivChatEntry
					{
						Type = Configuration.ChatChannelToUseForNotReadyMessage,
						Message = new SeString( new List<Payload>
						{
							//Dalamud.Game.Text.SeStringHandling.SeString.TextArrowPayloads,
							OpenReadyCheckWindowLink,
							new TextPayload( notReadyString ),
							RawPayload.LinkTerminator
						} )
					};
					Chat.Print( chatEntry );
				} );
			}
		}

		private void ShowBestAvailableReadyCheckWindow()
		{
			unsafe
			{
				var pReadyCheckNotification = (AtkUnitBase*)GameGui.GetAddonByName( "_NotificationReadyCheck" );
				if( false /*(IntPtr)pReadyCheckNotification != IntPtr.Zero && pReadyCheckNotification->IsVisible*/ )
				{
					//***** TODO: Try to show built in ready check window.  The addon doesn't exist unless it's opened, so this might be difficult. *****
				}

				PluginUi.ReadyCheckResultsWindowVisible = true;
			}
		}

		private void OnConditionChanged( ConditionFlag flag, bool value )
		{
			if( flag == ConditionFlag.InCombat )
			{
				if( value )
				{
					if( Configuration.ClearReadyCheckOverlayInCombat )
					{
						PluginUi.InvalidateReadyCheck();
					}
					else if( Configuration.ClearReadyCheckOverlayInCombatInInstancedCombat && mInstancedTerritories.Contains( ClientState.TerritoryType ) )
					{
						PluginUi.InvalidateReadyCheck();
					}
				}
			}
		}

		private void OnTerritoryChanged(UInt16 ID )
		{
			if( Configuration.ClearReadyCheckOverlayEnteringInstance && mInstancedTerritories.Contains( ID ) ) PluginUi.InvalidateReadyCheck();
		}

		private void OnLogout()
		{
			ReadyCheckActive = false;
			mTimedOverlayCancellationSource?.Cancel();
			PluginUi.InvalidateReadyCheck();
			mProcessedReadyCheckData = null;
		}

		internal List<CorrelatedReadyCheckEntry> GetProcessedReadyCheckData()
		{
			lock( mProcessedReadyCheckDataLockObj )
			{
				return mProcessedReadyCheckData != null ? new List<CorrelatedReadyCheckEntry>( mProcessedReadyCheckData ) : null;
			}
		}

		private void PopulateInstancedTerritoriesList()
		{
			var contentFinderSheet = DataManager.GetExcelSheet<ContentFinderCondition>()!;
			foreach(var zone in contentFinderSheet)
				mInstancedTerritories.Add(zone.TerritoryType.Row);
		}
	}
}
