using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using GateAlert.Core;

namespace GateAlert;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Master switch. Off means the watcher stays hooked but says nothing.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Watch every GATE instead of only the ticked ones.</summary>
    public bool AllGates { get; set; } = true;

    /// <summary>GATE names to watch when <see cref="AllGates"/> is off.</summary>
    public List<string> SelectedGates { get; set; } = [];

    /// <summary>Extra names, comma separated, matched as case-insensitive substrings.</summary>
    public string CustomGates { get; set; } = string.Empty;

    /// <summary>Only shout while you are actually inside the Gold Saucer.</summary>
    public bool OnlyInGoldSaucer { get; set; } = true;

    /// <summary>How many chat lines an alert produces, including the first.</summary>
    public int RepeatCount { get; set; } = 3;

    /// <summary>Seconds between the reminder lines.</summary>
    public int RepeatIntervalSeconds { get; set; } = 45;

    /// <summary>Also throw a screen toast (off by default: chat is the requested output).</summary>
    public bool ShowToast { get; set; }

    /// <summary>Append a bit of flavour to the alert line.</summary>
    public bool PlayfulFlavor { get; set; } = true;

    /// <summary>UIColor palette key for the alert accent (60 = the yellow Dalamud's own sample uses).</summary>
    public ushort AccentColorKey { get; set; } = 60;

    /// <summary>Clamp and sanitize values after load or before save.</summary>
    public void Sanitize()
    {
        RepeatCount = Math.Clamp(RepeatCount, 1, 10);
        RepeatIntervalSeconds = Math.Clamp(RepeatIntervalSeconds, 5, 600);
        CustomGates ??= string.Empty;
        SelectedGates ??= [];
        SelectedGates = SelectedGates
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public GateWatchSettings ToSettings(uint territoryType) =>
        new(Enabled, OnlyInGoldSaucer, territoryType, AllGates, SelectedGates, CustomGates);

    public bool IsSelected(string gateName) =>
        SelectedGates.Contains(gateName, StringComparer.OrdinalIgnoreCase);

    public void SetSelected(string gateName, bool selected)
    {
        SelectedGates.RemoveAll(g => string.Equals(g, gateName, StringComparison.OrdinalIgnoreCase));
        if (selected && !string.IsNullOrWhiteSpace(gateName))
            SelectedGates.Add(gateName.Trim());
    }
}
