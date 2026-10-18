using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using GateAlert.Core;

namespace GateAlert;

/// <summary>
/// Hooks the chat log, recognises GATE announcements and hands the ones you care about
/// to <see cref="GateAnnouncer"/>, then nags you a couple more times.
/// </summary>
public sealed class GateWatcher : IDisposable
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(10);
    private const int HistoryLimit = 8;

    private readonly IChatGui chatGui;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly GateAnnouncer announcer;

    private readonly List<GateHistoryEntry> history = [];
    private DateTime lastShoutAt = DateTime.MinValue;
    private string lastShoutName = string.Empty;

    private int repeatsRemaining;
    private DateTime nextRepeatAt = DateTime.MaxValue;

    public GateWatcher(
        IChatGui chatGui,
        IClientState clientState,
        IFramework framework,
        IPluginLog log,
        Configuration configuration,
        GateAnnouncer announcer)
    {
        this.chatGui = chatGui;
        this.clientState = clientState;
        this.framework = framework;
        this.log = log;
        this.configuration = configuration;
        this.announcer = announcer;
    }

    public event Action? Changed;

    public GateAnnouncement? LastAnnouncement { get; private set; }

    public DateTime? LastAnnouncementAt { get; private set; }

    public int RepeatsRemaining => this.repeatsRemaining;

    public IReadOnlyList<GateHistoryEntry> History => this.history;

    /// <summary>Announcements seen and parsed since load, whether or not they were watched.</summary>
    public int SeenCount { get; private set; }

    public DateTime? LastSeenAt { get; private set; }

    public string? LastSeenName { get; private set; }

    public bool IsInGoldSaucer => this.clientState.TerritoryType == GateCatalog.GoldSaucerTerritoryId;

    public void Start()
    {
        this.chatGui.ChatMessage += this.OnChatMessage;
        this.framework.Update += this.OnFrameworkUpdate;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        try
        {
            var sender = message.Sender?.ToString();
            var text = message.Message?.ToString();

            var extra = GateMatcher.CustomTokens(this.configuration.CustomGates)
                .Concat(this.configuration.SelectedGates.Where(g => !string.IsNullOrWhiteSpace(g)))
                .ToList();

            if (!GateAnnouncementParser.TryParse(sender, text, out var announcement, extra))
                return;

            this.SeenCount++;
            this.LastSeenAt = DateTime.UtcNow;
            this.LastSeenName = announcement.GateName;

            if (!GateMatcher.ShouldAnnounce(announcement, this.configuration.ToSettings(this.clientState.TerritoryType)))
            {
                this.Changed?.Invoke();
                return;
            }

            // The attendant can repeat a line inside a window; don't double-shout.
            if (string.Equals(announcement.GateName, this.lastShoutName, StringComparison.OrdinalIgnoreCase)
                && DateTime.UtcNow - this.lastShoutAt < DuplicateWindow)
                return;

            this.Fire(announcement, forced: false);
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "GateAlert: chat handler blew up");
        }
    }

    /// <summary>The exact text the alert would print, for the window preview.</summary>
    public string PreviewText(GateAnnouncement announcement) =>
        this.announcer.BuildPlainText(announcement, this.configuration.PlayfulFlavor, flavor: this.announcer.PreviewFlavor);

    /// <summary>Used by the test button/command so the output can be checked without waiting for a GATE.</summary>
    public void FireTest(string gateName, string location)
    {
        var name = string.IsNullOrWhiteSpace(gateName) ? GateCatalog.KnownGates[2] : gateName.Trim();
        if (name.Length > 64)
            name = name[..64];

        var where = string.IsNullOrWhiteSpace(location) ? "the Gold Saucer" : location.Trim();
        if (where.Length > 64)
            where = where[..64];

        this.Fire(new GateAnnouncement(name, where, GateCatalog.AttendantSender, "test"), forced: true);
    }

    private void Fire(GateAnnouncement announcement, bool forced)
    {
        this.LastAnnouncement = announcement;
        this.LastAnnouncementAt = DateTime.UtcNow;
        this.lastShoutName = announcement.GateName;
        this.lastShoutAt = DateTime.UtcNow;

        var total = Math.Clamp(this.configuration.RepeatCount, 1, 10);
        this.announcer.Announce(announcement, this.configuration, 1, total);

        this.history.Insert(0, new GateHistoryEntry(announcement.GateName, announcement.DisplayLocation, DateTime.UtcNow, forced));
        while (this.history.Count > HistoryLimit)
            this.history.RemoveAt(this.history.Count - 1);

        this.repeatsRemaining = total - 1;
        this.nextRepeatAt = DateTime.UtcNow.AddSeconds(Math.Clamp(this.configuration.RepeatIntervalSeconds, 5, 600));

        this.Changed?.Invoke();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (this.repeatsRemaining <= 0 || DateTime.UtcNow < this.nextRepeatAt)
            return;

        var announcement = this.LastAnnouncement;
        if (announcement is null
            || !GateMatcher.ShouldAnnounce(announcement, this.configuration.ToSettings(this.clientState.TerritoryType)))
        {
            this.repeatsRemaining = 0;
            this.Changed?.Invoke();
            return;
        }

        var total = Math.Clamp(this.configuration.RepeatCount, 1, 10);
        var index = total - this.repeatsRemaining + 1;
        this.announcer.Announce(announcement, this.configuration, index, total);

        this.repeatsRemaining--;
        this.nextRepeatAt = DateTime.UtcNow.AddSeconds(Math.Clamp(this.configuration.RepeatIntervalSeconds, 5, 600));
        this.Changed?.Invoke();
    }

    public void CancelRepeats()
    {
        this.repeatsRemaining = 0;
        this.Changed?.Invoke();
    }

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;
        this.chatGui.ChatMessage -= this.OnChatMessage;
    }
}

public sealed record GateHistoryEntry(string GateName, string Location, DateTime At, bool WasTest);
