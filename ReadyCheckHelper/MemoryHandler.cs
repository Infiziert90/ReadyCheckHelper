using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace ReadyCheckHelper
{
    public static class MemoryHandler
    {
        public static void Init()
        {
            try
            {
                // TODO: Replace with CS version after https://github.com/aers/FFXIVClientStructs/pull/882 got merged
                MfpOnReadyCheckInitiated = Plugin.SigScanner.ScanText("40 ?? 48 83 ?? ?? 48 8B ?? E8 ?? ?? ?? ?? 48 ?? ?? ?? 33 C0 ?? 89");
                MReadyCheckInitiatedHook = Plugin.Hook.HookFromAddress<ReadyCheckFuncDelegate>(MfpOnReadyCheckInitiated, ReadyCheckInitiatedDetour);
                MReadyCheckInitiatedHook.Enable();

                MfpOnReadyCheckEnd = Plugin.SigScanner.ScanText("40 ?? 53 48 ?? ?? ?? ?? 48 81 ?? ?? ?? ?? ?? 48 8B ?? ?? ?? ?? ?? 48 33 ?? ?? 89 ?? ?? ?? 83 ?? ?? ?? 48 8B ?? 75 ?? 48");
                MReadyCheckEndHook = Plugin.Hook.HookFromAddress<ReadyCheckFuncDelegate>(MfpOnReadyCheckEnd, ReadyCheckEndDetour);
                MReadyCheckEndHook.Enable();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while searching for required function signatures; this probably means that the plugin needs to be updated due to changes in Final Fantasy XIV.\n{ex}");
            }
        }

        public static void Uninit()
        {
            MReadyCheckInitiatedHook?.Disable();
            MReadyCheckInitiatedHook?.Dispose();
            MpReadyCheckObject = nint.Zero;
        }

        private static void ReadyCheckInitiatedDetour(nint ptr)
        {
            MReadyCheckInitiatedHook.Original(ptr);
            MpReadyCheckObject = ptr;
            ReadyCheckInitiatedEvent?.Invoke(null, EventArgs.Empty);
        }

        private static void ReadyCheckEndDetour(nint ptr)
        {
            MReadyCheckEndHook.Original(ptr);

            //	Do this for now because we don't get the ready check begin function called if we don't initiate ready check ourselves.
            MpReadyCheckObject = ptr;

            //	Update our copy of the data one last time.
            ReadyCheckCompleteEvent?.Invoke(null, EventArgs.Empty);
        }

        public static nint DEBUG_GetReadyCheckObjectAddress()
        {
            return MpReadyCheckObject;
        }

        public static void DEBUG_SetReadyCheckObjectAddress(nint ptr)
        {
            MpReadyCheckObject = ptr;
        }

        internal static unsafe PartyListLayoutResult? GetHUDIndicesForChar(ulong contentID, uint objectID)
        {
            if (contentID == 0 && objectID is 0 or 0xE0000000)
                return null;

            var infoProxyCrossRealm = InfoProxyCrossRealm.Instance();
            var groupManager = GroupManager.Instance();
            var agentHud = AgentHUD.Instance();
            if (infoProxyCrossRealm == null || groupManager == null)
                return null;

            //	We're only in a crossworld party if the cross realm proxy says we are; however, it can say we're cross-realm when
            //	we're in a regular party if we entered an instance as a cross-world party, so account for that too.
            if (groupManager->MemberCount > 0)
            {
                for (var i = 0; i < 8; ++i)
                {
                    var charData = groupManager->PartyMembersSpan[i];
                    if (contentID > 0 && contentID == (ulong) charData.ContentID)
                        return new PartyListLayoutResult(false, 0, i);

                    if (objectID > 0 && objectID != 0xE0000000 && objectID == charData.ObjectID)
                        return new PartyListLayoutResult(false, 0, i);
                }

                for (var i = 0; i < 40; ++i)
                {
                    if (objectID > 0 && objectID != 0xE0000000 && objectID == agentHud->RaidMemberIds[i])
                        return new PartyListLayoutResult(false, i / 8 + 1, i % 8);
                }
            }
            else if (infoProxyCrossRealm->IsCrossRealm > 0)
            {
                var pGroupMember = InfoProxyCrossRealm.GetMemberByContentId(contentID);
                if (pGroupMember == null || contentID == 0)
                    return null;
                return new PartyListLayoutResult(true, pGroupMember->GroupIndex, pGroupMember->MemberIndex);
            }

            return null;
        }

        //	Misc.
        private static nint MpReadyCheckObject;

        //	Delegates
        private delegate void ReadyCheckFuncDelegate(nint ptr);

        private static nint MfpOnReadyCheckInitiated = nint.Zero;
        private static Hook<ReadyCheckFuncDelegate> MReadyCheckInitiatedHook;

        private static nint MfpOnReadyCheckEnd = nint.Zero;
        private static Hook<ReadyCheckFuncDelegate> MReadyCheckEndHook;

        //	Events
        public static event EventHandler ReadyCheckInitiatedEvent;
        public static event EventHandler ReadyCheckCompleteEvent;
    }

    internal struct PartyListLayoutResult
    {
        internal PartyListLayoutResult(bool crossWorld, int groupNumber, int partyMemberIndex)
        {
            CrossWorld = crossWorld;
            GroupNumber = groupNumber;
            PartyMemberIndex = partyMemberIndex;
        }

        internal readonly bool CrossWorld;
        internal readonly int GroupNumber;
        internal readonly int PartyMemberIndex;
    }
}