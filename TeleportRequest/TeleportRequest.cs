using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace TeleportRequest;

[ApiVersion(2, 1)]
public class TeleportRequest : TerrariaPlugin
{
    private System.Timers.Timer Timer = null!;

    private readonly bool[] TPAutoDenies = new bool[256];

    private readonly bool[] TPAutoAccepts = new bool[256];

    private readonly TPRequest[] TPRequests = new TPRequest[256];

    public override string Name => "TeleportRequest";

    public override string Author => "Neoslyke, MarioE)";

    public override Version Version => new Version(2, 1, 0);

    public override string Description => "Adds teleportation accept commands.";

    public static Config Config { get; set; } = null!;

    internal static string ConfigPath => Path.Combine(TShock.SavePath, "TeleportRequest.json");

    public TeleportRequest(Main game)
        : base(game)
    {
        Config = new Config();
        for (var i = 0; i < this.TPRequests.Length; i++)
        {
            this.TPRequests[i] = new TPRequest();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.GameInitialize.Deregister(this, this.OnInitialize);
            ServerApi.Hooks.ServerLeave.Deregister(this, this.OnLeave);
            GeneralHooks.ReloadEvent -= this.OnReload;

            this.Timer.Dispose();
        }
    }

    public override void Initialize()
    {
        ServerApi.Hooks.GameInitialize.Register(this, this.OnInitialize);
        ServerApi.Hooks.ServerLeave.Register(this, this.OnLeave);
        GeneralHooks.ReloadEvent += this.OnReload;
    }

    private void OnElapsed(object? sender, ElapsedEventArgs e)
    {
        for (var i = 0; i < this.TPRequests.Length; i++)
        {
            var tpr = this.TPRequests[i];
            if (tpr.timeout <= 0)
            {
                continue;
            }
            var dst = TShock.Players[tpr.dst];
            var src = TShock.Players[i];

            if (dst == null || src == null)
            {
                tpr.timeout = 0;
                continue;
            }

            tpr.timeout--;
            if (tpr.timeout == 0)
            {
                src.SendErrorMessage("Your teleport request timed out.");
                dst.SendInfoMessage("{0}'s teleport request timed out.", src.Name);
                continue;
            }
            var msg = "{0} is requesting to teleport to you. (/tpaccept or /tpdeny)";
            if (tpr.dir)
            {
                msg = "You are requested to teleport to {0}. (/tpaccept or /tpdeny)";
            }
            dst.SendInfoMessage(msg, src.Name);
        }
    }

    private void OnInitialize(EventArgs e)
    {
        Commands.ChatCommands.Add(new Command("tprequest.accept", this.TPAccept, "tpaccept")
        {
            AllowServer = false,
            HelpText = "Accepts a teleport request."
        });
        Commands.ChatCommands.Add(new Command("tprequest.autoaccept", this.TPAutoAccept, "tpautoaccept")
        {
            AllowServer = false,
            HelpText = "Toggles automatic acceptance of teleport requests."
        });
        Commands.ChatCommands.Add(new Command("tprequest.autodeny", this.TPAutoDeny, "tpautodeny")
        {
            AllowServer = false,
            HelpText = "Toggles automatic denial of teleport requests."
        });
        Commands.ChatCommands.Add(new Command("tprequest.deny", this.TPDeny, "tpdeny")
        {
            AllowServer = false,
            HelpText = "Denies a teleport request."
        });
        Commands.ChatCommands.Add(new Command("tprequest.tpahere", this.TPAHere, "tpahere")
        {
            AllowServer = false,
            HelpText = "Sends a request for someone to teleport to you."
        });
        Commands.ChatCommands.Add(new Command("tprequest.tpa", this.TPA, "tpa")
        {
            AllowServer = false,
            HelpText = "Sends a request to teleport to someone."
        });
        this.SetupConfig();
        this.Timer = new System.Timers.Timer(Config.Interval * 1000);
        this.Timer.Elapsed += this.OnElapsed;
        this.Timer.Start();
    }

    private void OnLeave(LeaveEventArgs e)
    {
        this.TPAutoDenies[e.Who] = false;
        this.TPAutoAccepts[e.Who] = false;
        this.TPRequests[e.Who].timeout = 0;
    }

    private void TPA(CommandArgs e)
    {
        if (e.Parameters.Count == 0)
        {
            e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tpa <player>");
            return;
        }
        var search = string.Join(" ", e.Parameters.ToArray());
        var players = TSPlayer.FindByNameOrID(search);
        if (players.Count == 0)
        {
            e.Player.SendErrorMessage("Invalid player!");
            return;
        }
        if (players.Count > 1)
        {
            e.Player.SendErrorMessage("More than one player matched!");
            return;
        }
        if (players[0].Equals(e.Player))
        {
            e.Player.SendErrorMessage("You cannot send a teleport request to yourself!");
            return;
        }
        if ((!players[0].TPAllow || this.TPAutoDenies[players[0].Index]) && !e.Player.Group.HasPermission(Permissions.tpoverride))
        {
            e.Player.SendErrorMessage("You cannot teleport to {0}.", players[0].Name);
            return;
        }
        if ((players[0].TPAllow && this.TPAutoAccepts[players[0].Index]) || e.Player.Group.HasPermission(Permissions.tpoverride))
        {
            if (e.Player.Teleport(players[0].X, players[0].Y))
            {
                e.Player.SendSuccessMessage("Teleported to {0}.", players[0].Name);
                players[0].SendSuccessMessage("{0} teleported to you.", e.Player.Name);
            }
            return;
        }
        for (var i = 0; i < this.TPRequests.Length; i++)
        {
            var tpr = this.TPRequests[i];
            if (tpr.timeout > 0 && tpr.dst == players[0].Index)
            {
                e.Player.SendErrorMessage("{0} already has a teleport request.", players[0].Name);
                return;
            }
        }
        this.TPRequests[e.Player.Index].dir = false;
        this.TPRequests[e.Player.Index].dst = (byte)players[0].Index;
        this.TPRequests[e.Player.Index].timeout = Config.Timeout + 1;
        e.Player.SendSuccessMessage("Sent a teleport request to {0}.", players[0].Name);
    }

    private void TPAccept(CommandArgs e)
    {
        for (var i = 0; i < this.TPRequests.Length; i++)
        {
            var tpr = this.TPRequests[i];
            if (tpr.timeout > 0 && tpr.dst == e.Player.Index)
            {
                var plr1 = tpr.dir ? e.Player : TShock.Players[i];
                var plr2 = tpr.dir ? TShock.Players[i] : e.Player;
                if (plr1 != null && plr2 != null && plr1.Teleport(plr2.X, plr2.Y))
                {
                    plr1.SendSuccessMessage("Teleported to {0}.", plr2.Name);
                    plr2.SendSuccessMessage("{0} teleported to you.", plr1.Name);
                }
                tpr.timeout = 0;
                return;
            }
        }
        e.Player.SendErrorMessage("You have no pending teleport requests.");
    }

    private void TPAHere(CommandArgs e)
    {
        if (e.Parameters.Count == 0)
        {
            e.Player.SendErrorMessage("Invalid syntax! Proper syntax: /tpahere <player>");
            return;
        }
        var search = string.Join(" ", e.Parameters.ToArray());
        var players = TSPlayer.FindByNameOrID(search);
        if (players.Count == 0)
        {
            e.Player.SendErrorMessage("Invalid player!");
            return;
        }
        if (players.Count > 1)
        {
            e.Player.SendErrorMessage("More than one player matched!");
            return;
        }
        if (players[0].Equals(e.Player))
        {
            e.Player.SendErrorMessage("You cannot send a teleport request to yourself!");
            return;
        }
        if ((!players[0].TPAllow || this.TPAutoDenies[players[0].Index]) && !e.Player.Group.HasPermission(Permissions.tpoverride))
        {
            e.Player.SendErrorMessage("You cannot teleport {0}.", players[0].Name);
            return;
        }
        if ((players[0].TPAllow && this.TPAutoAccepts[players[0].Index]) || e.Player.Group.HasPermission(Permissions.tpoverride))
        {
            if (players[0].Teleport(e.Player.X, e.Player.Y))
            {
                players[0].SendSuccessMessage("Teleported to {0}.", e.Player.Name);
                e.Player.SendSuccessMessage("{0} teleported to you.", players[0].Name);
            }
            return;
        }
        for (var i = 0; i < this.TPRequests.Length; i++)
        {
            var tpr = this.TPRequests[i];
            if (tpr.timeout > 0 && tpr.dst == players[0].Index)
            {
                e.Player.SendErrorMessage("{0} already has a teleport request.", players[0].Name);
                return;
            }
        }
        this.TPRequests[e.Player.Index].dir = true;
        this.TPRequests[e.Player.Index].dst = (byte)players[0].Index;
        this.TPRequests[e.Player.Index].timeout = Config.Timeout + 1;
        e.Player.SendSuccessMessage("Sent a teleport request to {0}.", players[0].Name);
    }

    private void TPAutoAccept(CommandArgs e)
    {
        if (this.TPAutoAccepts[e.Player.Index])
        {
            e.Player.SendErrorMessage("You already have auto-accept enabled. Use /tpautodeny to switch.");
            return;
        }
        if (this.TPAutoDenies[e.Player.Index])
        {
            this.TPAutoDenies[e.Player.Index] = false;
            this.TPAutoAccepts[e.Player.Index] = true;
            e.Player.SendInfoMessage("Switched from auto-deny to auto-accept.");
            return;
        }
        this.TPAutoAccepts[e.Player.Index] = true;
        e.Player.SendInfoMessage("Enabled automatic TP acceptance.");
    }

    private void TPAutoDeny(CommandArgs e)
    {
        if (this.TPAutoDenies[e.Player.Index])
        {
            e.Player.SendErrorMessage("You already have auto-deny enabled. Use /tpautoaccept to switch.");
            return;
        }
        if (this.TPAutoAccepts[e.Player.Index])
        {
            this.TPAutoAccepts[e.Player.Index] = false;
            this.TPAutoDenies[e.Player.Index] = true;
            e.Player.SendInfoMessage("Switched from auto-accept to auto-deny.");
            return;
        }
        this.TPAutoDenies[e.Player.Index] = true;
        e.Player.SendInfoMessage("Enabled automatic TP denial.");
    }

    private void TPDeny(CommandArgs e)
    {
        for (var i = 0; i < this.TPRequests.Length; i++)
        {
            var tpr = this.TPRequests[i];
            if (tpr.timeout > 0 && tpr.dst == e.Player.Index)
            {
                var player = TShock.Players[i];
                if (player != null)
                {
                    e.Player.SendSuccessMessage("Denied {0}'s teleport request.", player.Name);
                    player.SendErrorMessage("{0} denied your teleport request.", e.Player.Name);
                }
                tpr.timeout = 0;
                return;
            }
        }
        e.Player.SendErrorMessage("You have no pending teleport requests.");
    }

    private void SetupConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                Config = Config.Read(ConfigPath);
            }
            Config.Write(ConfigPath);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError("TeleportRequest config error:");
            TShock.Log.ConsoleError(ex.ToString());
        }
    }

    private void OnReload(ReloadEventArgs args)
    {
        this.SetupConfig();
        args.Player?.SendSuccessMessage("[TeleportRequest] Config reloaded.");
    }
}