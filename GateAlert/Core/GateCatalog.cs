namespace GateAlert.Core;

/// <summary>
/// The GATEs the Gold Saucer can run. Names are the ones the Gold Saucer Attendant
/// announces, so they are also what the chat matcher compares against.
/// </summary>
public static class GateCatalog
{
    /// <summary>TerritoryType row id for The Gold Saucer (verified against game data).</summary>
    public const uint GoldSaucerTerritoryId = 144;

    /// <summary>Sender name of the NPC that announces GATEs in the saucer.</summary>
    public const string AttendantSender = "Gold Saucer Attendant";

    public static readonly string[] KnownGates =
    [
        "Air Force One",
        "Any Way the Wind Blows",
        "Cliffhanger",
        "Leap of Faith",
        "The Slice Is Right",
    ];
}
