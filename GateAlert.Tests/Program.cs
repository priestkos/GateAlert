using System;
using System.Collections.Generic;
using System.Linq;
using GateAlert.Core;

/// <summary>
/// Runnable checks for the announcement parser and the watch matcher.
/// The real announcement wording cannot be generated outside the game, so the
/// samples here mirror the shapes the Gold Saucer Attendant uses.
/// Run: dotnet run --project GateAlert.Tests
/// </summary>
internal static class Program
{
    private const string Attendant = "Gold Saucer Attendant";

    private static int passed;
    private static int failed;

    private static int Main()
    {
        // ---- parsing: the shapes we expect to recognise ----
        Parses(Attendant, "A limited-time event \u201cCliffhanger\u201d is now underway in the Event Square.",
            "Cliffhanger", "the Event Square");

        Parses(Attendant, "A limited-time event \"Any Way the Wind Blows\" is now underway in the Wonder Square.",
            "Any Way the Wind Blows", "the Wonder Square");

        Parses(Attendant, "A limited-time event \u201cAir Force One\u201d is now underway in the Airship Landing.",
            "Air Force One", "the Airship Landing");

        Parses(Attendant, "A limited-time event \u201cLeap of Faith\u201d is now underway in the Gold Saucer.",
            "Leap of Faith", "the Gold Saucer");

        Parses(Attendant, "The GATE \u201cThe Slice Is Right\u201d has begun in the Round Square.",
            "The Slice Is Right", "the Round Square");

        // No quotes at all, but the name is one we know and the line is a marker.
        Parses(Attendant, "A limited-time event, Cliffhanger, is now underway in the Event Square.",
            "Cliffhanger", "the Event Square");

        // Non-breaking space, the kind the client sprinkles into announcements.
        Parses(Attendant, "A limited-time\u00a0event \u201cCliffhanger\u201d is now underway in the Event Square.",
            "Cliffhanger", "the Event Square");

        // User-supplied GATE name, announced by the attendant without an announcement phrase.
        Parses(Attendant, "The \u201cBlunderville Sprint\u201d GATE is now open!",
            "Blunderville Sprint", string.Empty, ["Blunderville Sprint"]);

        // ---- parsing: what must NOT fire ----
        Rejects("Random Player", "That \"Cliffhanger\" was fun, eh?");
        Rejects(Attendant, "Please visit the \u201cPrize Claim\u201d counter when you have a moment.");
        Rejects(Attendant, "Speak with the \u201cGate Keeper\u201d for details on upcoming events.");
        Rejects(Attendant, string.Empty);
        Rejects(Attendant, "A limited-time event is now underway in the Event Square.");

        // ---- matching ----
        var cliffhanger = new GateAnnouncement("Cliffhanger", "the Event Square", Attendant, "x");
        var wind = new GateAnnouncement("Any Way the Wind Blows", "the Wonder Square", Attendant, "x");

        var inSaucer = GateCatalog.GoldSaucerTerritoryId;
        var elsewhere = 129u; // Limsa Lominsa Lower Decks

        Check("all gates watches Cliffhanger", GateMatcher.ShouldAnnounce(cliffhanger, new GateWatchSettings(true, true, inSaucer, true, [], string.Empty)));
        Check("all gates watches the wind", GateMatcher.ShouldAnnounce(wind, new GateWatchSettings(true, true, inSaucer, true, [], string.Empty)));
        Check("paused plugin says nothing", !GateMatcher.ShouldAnnounce(cliffhanger, new GateWatchSettings(false, true, inSaucer, true, [], string.Empty)));
        Check("outside the saucer says nothing", !GateMatcher.ShouldAnnounce(cliffhanger, new GateWatchSettings(true, true, elsewhere, true, [], string.Empty)));
        Check("outside the saucer still fires when the guard is off", GateMatcher.ShouldAnnounce(cliffhanger, new GateWatchSettings(true, false, elsewhere, true, [], string.Empty)));
        Check("only the ticked gate fires", GateMatcher.ShouldAnnounce(cliffhanger, new GateWatchSettings(true, true, inSaucer, false, ["Cliffhanger"], string.Empty)));
        Check("a GATE that is not ticked stays quiet", !GateMatcher.ShouldAnnounce(wind, new GateWatchSettings(true, true, inSaucer, false, ["Cliffhanger"], string.Empty)));
        Check("several ticked gates all fire", GateMatcher.ShouldAnnounce(wind, new GateWatchSettings(true, true, inSaucer, false, ["Cliffhanger", "Any Way the Wind Blows"], string.Empty)));
        Check("custom comma list matches any token in it", GateMatcher.ShouldAnnounce(wind, new GateWatchSettings(true, true, inSaucer, false, [], "cliffhanger, any way the wind")));
        Check("custom comma list matches the second token", GateMatcher.ShouldAnnounce(new GateAnnouncement("The Slice Is Right", "the Round Square", Attendant, "x"), new GateWatchSettings(true, true, inSaucer, false, [], "cliffhanger, the slice")));
        Check("an unlisted GATE stays quiet", !GateMatcher.ShouldAnnounce(wind, new GateWatchSettings(true, true, inSaucer, false, [], "cliffhanger, the slice")));
        Check("custom list is case-insensitive substring", GateMatcher.ShouldAnnounce(new GateAnnouncement("Leap of Faith", "the Event Square", Attendant, "x"), new GateWatchSettings(true, true, inSaucer, false, [], "leap")));
        Check("semicolon separated customs work", GateMatcher.CustomTokens("a; b , c").SequenceEqual(["a", "b", "c"]));
        Check("no match is a no-op", !GateMatcher.ShouldAnnounce(wind, new GateWatchSettings(true, true, inSaucer, false, ["Cliffhanger"], "cliffhanger")));

        Console.WriteLine($"\n{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static void Parses(string sender, string message, string expectedName, string expectedLocation, IReadOnlyCollection<string>? extra = null)
    {
        if (!GateAnnouncementParser.TryParse(sender, message, out var parsed, extra))
        {
            Fail($"parse \"{Shorten(message)}\" -> expected {expectedName}, got nothing");
            return;
        }

        var nameOk = parsed.GateName == expectedName;
        var locationOk = parsed.Location == expectedLocation;
        if (nameOk && locationOk)
            Pass($"parse \"{Shorten(message)}\" -> {parsed.GateName} @ {parsed.Location}");
        else
            Fail($"parse \"{Shorten(message)}\" -> got {parsed.GateName} @ \"{parsed.Location}\", wanted {expectedName} @ \"{expectedLocation}\"");
    }

    private static void Rejects(string sender, string message)
    {
        if (GateAnnouncementParser.TryParse(sender, message, out var parsed))
            Fail($"\"{Shorten(message)}\" from {sender} should be ignored, parsed as {parsed.GateName}");
        else
            Pass($"ignored \"{Shorten(message)}\" from {sender}");
    }

    private static void Check(string what, bool ok)
    {
        if (ok)
            Pass(what);
        else
            Fail(what);
    }

    private static void Pass(string what)
    {
        passed++;
        Console.WriteLine($"  ok   {what}");
    }

    private static void Fail(string what)
    {
        failed++;
        Console.WriteLine($"  FAIL {what}");
    }

    private static string Shorten(string value) => value.Length <= 58 ? value : value[..55] + "...";
}