using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace ReadyCheckHelper;

//	A struct to hold data for a player involved in the ready check.  This isn't a game construct, but something
//	for our data model since the game has this information scattered in annoying to use ways.
internal struct CorrelatedReadyCheckEntry
{
	internal CorrelatedReadyCheckEntry( string name, ulong contentId, uint entityId, ReadyCheckStatus readyState, byte groupIndex, byte memberIndex )
	{
		Name = name;
		ContentId = contentId;
		EntityId = entityId;
		ReadyState = readyState;
		GroupIndex = groupIndex;
		MemberIndex = memberIndex;
	}

	internal string Name { get; private set; }
	internal ulong ContentId { get; private set; }
	internal uint EntityId { get; private set; }
	internal ReadyCheckStatus ReadyState { get; private set; }
	internal byte GroupIndex { get; private set; }
	internal byte MemberIndex { get; private set; }	//	Take care using this; it can be very misleading.
}
