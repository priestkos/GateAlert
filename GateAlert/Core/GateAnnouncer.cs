using System;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.Toast;
using GateAlert.Core;

namespace GateAlert;

/// <summary>Builds and emits the alert line.</summary>
public sealed class GateAnnouncer(IChatGui chatGui, IToastGui toastGui, IPluginLog log)
{
    private static readonly string[] Flavors =
    [
        "go go go!",
        "the saucer calls.",
        "MGP will not earn itself.",
        "your cue, adventurer.",
    ];

    private readonly Random random = new();

    /// <summary>Stable flavour for the window preview, so it doesn't flicker every frame.</summary>
    public string PreviewFlavor => Flavors[0];

    public string BuildPlainText(GateAnnouncement announcement, bool playful, int? reminderIndex = null, int? reminderTotal = null, string? flavor = null)
    {
        var prefix = reminderIndex is { } index && reminderTotal is { } total && total > 1
            ? $"GATE ALERT ({index}/{total}): "
            : "GATE ALERT: ";

        var line = $"{prefix}\u2605 {announcement.GateName} \u2014 {announcement.DisplayLocation}";
        if (playful)
            line += $"  ({flavor ?? Flavors[random.Next(Flavors.Length)]})";

        return line;
    }

    /// <summary>Loud gold chat line, with the game's own gold star icon in front of it.</summary>
    public void Announce(GateAnnouncement announcement, Configuration configuration, int? reminderIndex = null, int? reminderTotal = null)
    {
        try
        {
            var builder = new SeStringBuilder();
            builder.AddIcon(BitmapFontIcon.GoldStar);
            builder.AddUiForeground(configuration.AccentColorKey);

            var headline = reminderIndex is { } index && reminderTotal is { } total && total > 1
                ? $" GATE ALERT {index}/{total} "
                : " GATE ALERT ";

            builder.AddText(headline);
            builder.AddUiForegroundOff();
            builder.AddText($" \u201c{announcement.GateName}\u201d is up \u2014 {announcement.DisplayLocation}");

            if (configuration.PlayfulFlavor)
                builder.AddItalics($"  ({Flavors[random.Next(Flavors.Length)]})");

            chatGui.Print(builder.Build(), "GATE", configuration.AccentColorKey);

            if (configuration.ShowToast)
                toastGui.ShowNormal(BuildPlainText(announcement, configuration.PlayfulFlavor, reminderIndex, reminderTotal), new ToastOptions());
        }
        catch (Exception ex)
        {
            log.Error(ex, "GateAlert: failed to print the alert line");
        }
    }
}
