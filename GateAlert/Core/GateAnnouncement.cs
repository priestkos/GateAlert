using System;
using System.Collections.Generic;
using System.Linq;

namespace GateAlert.Core;

/// <summary>A parsed "a GATE just opened" announcement.</summary>
public sealed record GateAnnouncement(string GateName, string Location, string Sender, string RawMessage)
{
    /// <summary>How the alert reads when no location was announced.</summary>
    public string DisplayLocation => string.IsNullOrWhiteSpace(Location) ? "the Gold Saucer" : Location;
}

/// <summary>
/// Turns the Gold Saucer Attendant's chat line into a <see cref="GateAnnouncement"/>.
/// Pure and Dalamud-free so it can be unit tested without the game.
///
/// The live EN line reads roughly:
///   A limited-time event “Cliffhanger” is now underway in the Event Square.
/// </summary>
public static class GateAnnouncementParser
{
    private static readonly char[] OpenQuotes = ['“', '"', '「', '『', '«', '„'];
    private static readonly char[] CloseQuotes = ['”', '"', '」', '』', '»'];

    private static readonly string[] AnnouncementMarkers =
    [
        "limited-time event",
        "limited time event",
        "special event",
        "is now underway",
    ];

    /// <summary>
    /// True when the Gold Saucer Attendant announced a GATE and a name could be read out of it.
    /// <paramref name="extraNames"/> lets user-supplied names count as known GATEs.
    /// Player chat is never accepted, even when it copies the announcement wording.
    /// </summary>
    public static bool TryParse(
        string? sender,
        string? message,
        out GateAnnouncement announcement,
        IReadOnlyCollection<string>? extraNames = null)
    {
        announcement = null!;

        var text = Normalize(message);
        if (text.Length == 0)
            return false;

        // Exact attendant only — substring would let forged sender names through.
        var fromAttendant = IsAttendant(sender);
        if (!fromAttendant)
            return false;

        var marked = AnnouncementMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));

        var name = ExtractQuotedName(text);
        if (name.Length == 0)
            name = ExtractKnownGateName(text, extraNames);

        if (name.Length == 0)
            return false;

        // Attendant + announcement phrasing, or attendant naming a GATE we know (built-in or custom).
        var known = IsKnownGate(name, extraNames);
        if (!marked && !known)
            return false;

        announcement = new GateAnnouncement(name, ExtractLocation(text), sender!.Trim(), text);
        return true;
    }

    /// <summary>True when <paramref name="sender"/> is exactly the Gold Saucer Attendant.</summary>
    public static bool IsAttendant(string? sender) =>
        !string.IsNullOrWhiteSpace(sender)
        && string.Equals(sender.Trim(), GateCatalog.AttendantSender, StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownGate(string name, IReadOnlyCollection<string>? extraNames)
    {
        if (GateCatalog.KnownGates.Any(g => string.Equals(name, g, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (extraNames is null)
            return false;

        foreach (var extra in extraNames)
        {
            if (string.IsNullOrWhiteSpace(extra))
                continue;

            var token = extra.Trim();
            if (token.Length < GateMatcher.MinCustomTokenLength)
                continue;

            // Exact match, or the extracted name contains the custom token (same rule as the matcher).
            if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase)
                || name.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Folds unicode oddities and whitespace so matching does not depend on them.</summary>
    private static string Normalize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var text = message.Replace('\u00a0', ' ').Replace('\u3000', ' ').Replace("’", "'");
        return text.Trim();
    }

    /// <summary>First quoted span, with the quotes stripped and whitespace collapsed.</summary>
    private static string ExtractQuotedName(string text)
    {
        var open = text.IndexOfAny(OpenQuotes);
        while (open >= 0)
        {
            var close = text.IndexOfAny(CloseQuotes, open + 1);
            if (close > open + 1)
            {
                var candidate = Collapse(text[(open + 1)..close]);
                if (candidate.Length > 0)
                    return candidate;
            }

            open = open + 1 < text.Length ? text.IndexOfAny(OpenQuotes, open + 1) : -1;
        }

        return string.Empty;
    }

    /// <summary>Fallback for lines that name the GATE without quoting it.</summary>
    private static string ExtractKnownGateName(string text, IReadOnlyCollection<string>? extraNames)
    {
        foreach (var gate in GateCatalog.KnownGates.Concat(CustomSafe(extraNames)))
            if (gate.Length > 0 && text.Contains(gate, StringComparison.OrdinalIgnoreCase))
                return gate;

        return string.Empty;
    }

    private static IEnumerable<string> CustomSafe(IReadOnlyCollection<string>? extraNames) =>
        extraNames?
            .Where(g => !string.IsNullOrWhiteSpace(g) && g.Trim().Length >= GateMatcher.MinCustomTokenLength)
            .Select(g => g.Trim())
        ?? [];

    /// <summary>Everything after the last " in ", minus trailing punctuation.</summary>
    private static string ExtractLocation(string text)
    {
        var index = text.LastIndexOf(" in ", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return string.Empty;

        var location = text[(index + 4)..].Trim().TrimEnd('.', '!', '?', ' ');
        return Collapse(location);
    }

    private static string Collapse(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }
}

/// <summary>The settings the matcher needs, kept as a plain value so it stays testable.</summary>
public readonly record struct GateWatchSettings(
    bool Enabled,
    bool OnlyInGoldSaucer,
    uint TerritoryType,
    bool AllGates,
    IReadOnlyCollection<string> SelectedGates,
    string CustomGates)
{
    public static GateWatchSettings Disabled => new(false, true, GateCatalog.GoldSaucerTerritoryId, true, [], string.Empty);
}

/// <summary>Decides whether an announcement should be shouted about.</summary>
public static class GateMatcher
{
    /// <summary>Custom tokens shorter than this are ignored (blocks "" / "a" false positives).</summary>
    public const int MinCustomTokenLength = 3;

    /// <summary>Comma/semicolon separated extras, matched as case-insensitive substrings.</summary>
    public static IEnumerable<string> CustomTokens(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length >= MinCustomTokenLength);

    /// <summary>Is this GATE one of the watched ones?</summary>
    public static bool IsWatched(string? gateName, bool allGates, IReadOnlyCollection<string>? selectedGates, string? customCsv)
    {
        if (string.IsNullOrWhiteSpace(gateName))
            return false;

        if (allGates)
            return true;

        if (selectedGates is not null
            && selectedGates.Any(g =>
                !string.IsNullOrWhiteSpace(g)
                && string.Equals(gateName, g.Trim(), StringComparison.OrdinalIgnoreCase)))
            return true;

        return CustomTokens(customCsv).Any(t => gateName.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The whole decision: enabled, in the right place, watching that GATE.</summary>
    public static bool ShouldAnnounce(GateAnnouncement? announcement, GateWatchSettings settings)
    {
        if (announcement is null || !settings.Enabled)
            return false;

        if (settings.OnlyInGoldSaucer && settings.TerritoryType != GateCatalog.GoldSaucerTerritoryId)
            return false;

        return IsWatched(announcement.GateName, settings.AllGates, settings.SelectedGates, settings.CustomGates);
    }
}
