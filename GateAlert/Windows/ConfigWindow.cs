using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using GateAlert.Core;

namespace GateAlert.Windows;

/// <summary>
/// The plugin's only window: a status board, the GATE picker and a live preview of the
/// chat line the plugin is about to shout.
/// </summary>
public sealed class ConfigWindow : Window, IDisposable
{
    private static readonly Vector4 Gold = new(0.96f, 0.78f, 0.29f, 1f);
    private static readonly Vector4 GoldDim = new(0.96f, 0.78f, 0.29f, 0.55f);
    private static readonly Vector4 Armed = new(0.42f, 0.86f, 0.47f, 1f);
    private static readonly Vector4 Paused = new(0.62f, 0.62f, 0.66f, 1f);
    private static readonly Vector4 Muted = new(0.66f, 0.66f, 0.72f, 1f);
    private static readonly Vector4 Panel = new(0.11f, 0.11f, 0.13f, 0.85f);
    private static readonly Vector4 PanelHover = new(0.16f, 0.16f, 0.19f, 0.95f);

    private readonly Configuration configuration;
    private readonly GateWatcher watcher;
    private readonly Action save;

    private string testGateName = GateCatalog.KnownGates[2];
    private string testLocation = "the Event Square";

    public ConfigWindow(Configuration configuration, GateWatcher watcher, Action save)
        : base("Gate Alert###gatealert", ImGuiWindowFlags.NoCollapse)
    {
        this.configuration = configuration;
        this.watcher = watcher;
        this.save = save;

        this.Size = new Vector2(430, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.RespectCloseHotkey = false;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Panel);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, PanelHover);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Gold);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Gold);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Gold);

        this.DrawHeader();
        ImGui.Separator();
        this.DrawGatePicker();
        ImGui.Separator();
        this.DrawOutput();
        ImGui.Separator();
        this.DrawNags();
        ImGui.Separator();
        this.DrawPreview();
        ImGui.Separator();
        this.DrawHistory();
        ImGui.Separator();
        this.DrawFooter();

        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(3);
    }

    private void DrawHeader()
    {
        var t = (float)ImGui.GetTime();
        var pulse = 0.6f + 0.4f * (float)Math.Sin(t * 3.2);

        var armed = this.configuration.Enabled;
        var statusColor = armed ? Armed : Paused;

        ImGui.TextColored(Gold, "GATE ALERT");
        ImGui.SameLine();

        var pillText = armed ? "  ARMED  " : "  PAUSED  ";
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(statusColor.X, statusColor.Y, statusColor.Z, armed ? 0.30f + 0.35f * pulse : 0.22f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(statusColor.X, statusColor.Y, statusColor.Z, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
        if (ImGui.Button(pillText))
        {
            this.configuration.Enabled = !this.configuration.Enabled;
            this.save();
        }

        ImGui.PopStyleColor(3);

        var here = this.watcher.IsInGoldSaucer;
        ImGui.SameLine();
        ImGui.TextColored(here ? GoldDim : Muted, here ? "you are in the saucer" : "not in the saucer");

        ImGui.TextColored(Muted, this.configuration.Enabled
            ? "Watching the Gold Saucer Attendant for your GATE."
            : "Paused \u2014 announcements are ignored until you arm it again.");
    }

    private void DrawGatePicker()
    {
        ImGui.TextColored(Gold, "WHAT TO WATCH");

        var all = this.configuration.AllGates;
        if (ImGui.Checkbox("Every GATE", ref all))
        {
            this.configuration.AllGates = all;
            this.save();
        }

        ImGui.SameLine();
        ImGui.TextColored(Muted, this.configuration.AllGates
            ? "all five, whatever the saucer rolls"
            : $"{this.configuration.SelectedGates.Count} selected");

        if (this.configuration.AllGates)
            ImGui.BeginDisabled();

        var avail = ImGui.GetContentRegionAvail().X;
        var x = 0f;
        foreach (var gate in GateCatalog.KnownGates)
        {
            var selected = this.configuration.IsSelected(gate);
            var size = ImGui.CalcTextSize(gate).X + 18f;

            if (x > 0 && x + size > avail)
            {
                x = 0f;
            }
            else if (x > 0)
            {
                ImGui.SameLine();
            }

            ImGui.PushStyleColor(ImGuiCol.Button, selected ? new Vector4(Gold.X, Gold.Y, Gold.Z, 0.30f) : Panel);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, selected ? new Vector4(Gold.X, Gold.Y, Gold.Z, 0.45f) : PanelHover);
            ImGui.PushStyleColor(ImGuiCol.Text, selected ? Gold : Muted);

            if (ImGui.Button($"{gate}##{gate}", new Vector2(size, 0)))
            {
                this.configuration.SetSelected(gate, !selected);
                this.save();
            }

            ImGui.PopStyleColor(3);
            x += size + 8f;
        }

        if (this.configuration.AllGates)
            ImGui.EndDisabled();

        var custom = this.configuration.CustomGates;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##customgates", "extra names, comma separated", ref custom, 256))
        {
            this.configuration.CustomGates = custom;
            this.save();
        }
    }

    private void DrawOutput()
    {
        ImGui.TextColored(Gold, "OUTPUT");

        var flavor = this.configuration.PlayfulFlavor;
        if (ImGui.Checkbox("Flavour text", ref flavor))
        {
            this.configuration.PlayfulFlavor = flavor;
            this.save();
        }

        ImGui.SameLine();
        var toast = this.configuration.ShowToast;
        if (ImGui.Checkbox("Also toast", ref toast))
        {
            this.configuration.ShowToast = toast;
            this.save();
        }

        var onlySaucer = this.configuration.OnlyInGoldSaucer;
        if (ImGui.Checkbox("Only while inside the Gold Saucer", ref onlySaucer))
        {
            this.configuration.OnlyInGoldSaucer = onlySaucer;
            this.save();
        }

        ImGui.TextColored(Muted, "Accent (chat colour palette key)");
        foreach (var (label, key) in new (string, ushort)[] { ("gold 60", 60), ("link 500", 500) })
        {
            var active = this.configuration.AccentColorKey == key;
            ImGui.PushStyleColor(ImGuiCol.Button, active ? new Vector4(Gold.X, Gold.Y, Gold.Z, 0.30f) : Panel);
            ImGui.PushStyleColor(ImGuiCol.Text, active ? Gold : Muted);
            if (ImGui.Button(label))
            {
                this.configuration.AccentColorKey = key;
                this.save();
            }

            ImGui.PopStyleColor(2);
            ImGui.SameLine();
        }

        var keyValue = (int)this.configuration.AccentColorKey;
        ImGui.SetNextItemWidth(90);
        if (ImGui.InputInt("##accentkey", ref keyValue))
        {
            this.configuration.AccentColorKey = (ushort)Math.Clamp(keyValue, 0, 65535);
            this.save();
        }

        ImGui.SameLine();
        ImGui.TextColored(Muted, "preview of the accent");
        ImGui.SameLine();
        ImGui.TextColored(Gold, "\u2605 GATE");
    }

    private void DrawNags()
    {
        ImGui.TextColored(Gold, "REMINDERS");

        var count = this.configuration.RepeatCount;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderInt("times", ref count, 1, 10))
        {
            this.configuration.RepeatCount = count;
            this.save();
        }

        var interval = this.configuration.RepeatIntervalSeconds;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderInt("every (s)", ref interval, 5, 300))
        {
            this.configuration.RepeatIntervalSeconds = interval;
            this.save();
        }

        if (this.watcher.RepeatsRemaining > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("stop nagging"))
                this.watcher.CancelRepeats();
        }
    }

    private void DrawPreview()
    {
        ImGui.TextColored(Gold, "WHAT LANDS IN CHAT");

        var sample = new GateAnnouncement(this.testGateName, this.testLocation, GateCatalog.AttendantSender, "preview");
        var line = this.watcher.PreviewText(sample);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Panel);
        if (ImGui.BeginChild("##preview", new Vector2(0, 44), false))
            ImGui.TextWrapped(line);
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.SetNextItemWidth(150);
        ImGui.InputText("##testgate", ref this.testGateName, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        ImGui.InputText("##testloc", ref this.testLocation, 64);
        ImGui.SameLine();
        if (ImGui.Button("test"))
            this.watcher.FireTest(this.testGateName, this.testLocation);
    }

    private void DrawHistory()
    {
        ImGui.TextColored(Gold, "RECENT");

        if (this.watcher.History.Count == 0)
        {
            ImGui.TextColored(Muted, this.watcher.SeenCount == 0
                ? "nothing seen yet \u2014 sit in the saucer and wait for the attendant"
                : $"{this.watcher.SeenCount} announcement(s) seen, none matched your filter");
            return;
        }

        foreach (var entry in this.watcher.History.Take(5))
        {
            var when = entry.At.ToString("HH:mm:ss");
            ImGui.TextColored(entry.WasTest ? Muted : Gold, $"{when}  {entry.GateName}");
            ImGui.SameLine();
            ImGui.TextColored(Muted, entry.WasTest ? "(test)" : entry.Location);
        }
    }

    private void DrawFooter()
    {
        if (ImGui.Button("Save & close"))
        {
            this.save();
            this.IsOpen = false;
        }

        ImGui.SameLine();
        if (ImGui.Button("Fire a test alert"))
            this.watcher.FireTest(this.testGateName, this.testLocation);

        ImGui.SameLine();
        ImGui.TextColored(Muted, $"/gatealert  \u2022  seen {this.watcher.SeenCount}");
    }
}
