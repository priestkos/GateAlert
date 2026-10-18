using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GateAlert.Core;
using GateAlert.Windows;

namespace GateAlert;

public sealed class GateAlertPlugin : IDalamudPlugin
{
    private const string CommandName = "/gatealert";
    private const string TestCommandName = "/gatetest";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;

    private readonly WindowSystem windowSystem = new("Gate Alert");
    private readonly Configuration configuration;
    private readonly GateAnnouncer announcer;
    private readonly GateWatcher watcher;
    private readonly ConfigWindow configWindow;

    public GateAlertPlugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IClientState clientState,
        IFramework framework,
        IToastGui toastGui,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;

        this.configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Sanitize();
        this.announcer = new GateAnnouncer(chatGui, toastGui, log);
        this.watcher = new GateWatcher(chatGui, clientState, framework, log, this.configuration, this.announcer);
        this.configWindow = new ConfigWindow(this.configuration, this.watcher, this.SaveConfiguration);

        this.windowSystem.AddWindow(this.configWindow);

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open the GATE alert window. Subcommands: on | off | test [GATE name]",
        });

        this.commandManager.AddHandler(TestCommandName, new CommandInfo(this.OnTestCommand)
        {
            HelpMessage = "Fire a test GATE alert so you can see the chat line.",
        });

        this.pluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenConfig;
        this.pluginInterface.UiBuilder.OpenMainUi += this.OpenConfig;

        this.watcher.Start();

        this.log.Information("Gate Alert loaded \u2014 watching the Gold Saucer Attendant");
    }

    private void OnCommand(string command, string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var verb = parts.Length > 0 ? parts[0] : string.Empty;

        if (string.Equals(verb, "on", StringComparison.OrdinalIgnoreCase))
        {
            this.configuration.Enabled = true;
            this.SaveConfiguration();
            return;
        }

        if (string.Equals(verb, "off", StringComparison.OrdinalIgnoreCase))
        {
            this.configuration.Enabled = false;
            this.SaveConfiguration();
            return;
        }

        if (string.Equals(verb, "test", StringComparison.OrdinalIgnoreCase))
        {
            var name = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;
            this.watcher.FireTest(name, "the Event Square");
            return;
        }

        this.configWindow.Toggle();
    }

    private void OnTestCommand(string command, string args) =>
        this.watcher.FireTest(args.Trim(), "the Event Square");

    private void OpenConfig() => this.configWindow.IsOpen = true;

    private void SaveConfiguration()
    {
        this.configuration.Sanitize();
        this.pluginInterface.SavePluginConfig(this.configuration);
    }

    public void Dispose()
    {
        this.watcher.Dispose();
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenConfig;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfig;
        this.pluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        this.commandManager.RemoveHandler(TestCommandName);
        this.commandManager.RemoveHandler(CommandName);
        this.windowSystem.RemoveAllWindows();
        this.configWindow.Dispose();
    }
}
