using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentReadyCheck;

namespace ReadyCheckHelper;

//	A struct to hold data for a player involved in the ready check.  This isn't a game construct, but something
//	for our data model since the game has this information scattered in annoying to use ways.
internal struct CorrelatedReadyCheckEntry
{
	internal CorrelatedReadyCheckEntry( string name, ulong contentID, uint objectID, ReadyCheckStatus readyState, byte groupIndex, byte memberIndex )
	{
		Name = name;
		ContentID = contentID;
		ObjectID = objectID;
		ReadyState = readyState;
		GroupIndex = groupIndex;
		MemberIndex = memberIndex;
	}

	internal string Name { get; private set; }
	internal ulong ContentID { get; private set; }
	internal uint ObjectID { get; private set; }
	internal ReadyCheckStatus ReadyState { get; private set; }
	internal byte GroupIndex { get; private set; }
	internal byte MemberIndex { get; private set; }	//	Take care using this; it can be very misleading.
}
