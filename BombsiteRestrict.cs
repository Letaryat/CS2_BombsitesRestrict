using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Commands;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using System.Drawing;


namespace BombsiteRestrict;
[MinimumApiVersion(242)]
public class Config : BasePluginConfig
{
    [JsonPropertyName("Minimum players")] public int iMinPlayers { get; set; } = 6;
    [JsonPropertyName("Count bots as players")] public bool bCountBots { get; set; } = false;
    [JsonPropertyName("Disabled site")] public int iDisabledSite { get; set; } = 0;
    [JsonPropertyName("Which team count as players")] public int iTeam { get; set; } = 0;
    [JsonPropertyName("Send plant restrict message to team")] public int iMessageTeam { get; set; } = 0;
    [JsonPropertyName("Center message timer")] public int iTimer { get; set; } = 15;
}

public class BombsiteRestrict : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Bombsite Restrict";
    public override string ModuleAuthor => "Nocky (SourceFactory.eu)";
    public override string ModuleVersion => "1.0.8";
    public Config Config { get; set; } = new Config();
    public void OnConfigParsed(Config config) { Config = config; }
    private static CounterStrikeSharp.API.Modules.Timers.Timer? hudTimer;
    private int disabledSite;
    private int teamMessages;
    private bool isPluginDisabled;
    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnTick>(() =>
        {
            if (disabledSite != 0)
            {
                var site = disabledSite == 1 ? "B" : "A";
                foreach (var p in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV))
                {
                    if (teamMessages == 3 || teamMessages == 2)
                    {
                        if (p.TeamNum == teamMessages)
                        {
                            p.PrintToCenterHtml($"{Localizer["Bombsite_Disabled_Center", site]}");
                        }
                    }
                    else
                    {
                        p.PrintToCenterHtml($"{Localizer["Bombsite_Disabled_Center", site]}");
                    }
                }
            }
        });

        RegisterListener<Listeners.OnMapEnd>(() =>
        {
            if (hudTimer != null)
                hudTimer.Kill();
        });
        RegisterListener<Listeners.OnMapStart>(mapName =>
        {
            Server.NextFrame(() =>
            {
                disabledSite = 0;
                teamMessages = Config.iMessageTeam;
                if (teamMessages == 1)
                    teamMessages = 3;
                else if (teamMessages == 2)
                    teamMessages = 2;


                var Bombsites = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_bomb_target");
                if (Bombsites.Count() != 2)
                {
                    isPluginDisabled = true;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[Bombsite Restrict] The Bombsite Restrict plugin is disabled, because there are no bomb plants on this map.");
                    Console.ResetColor();
                }
                else
                {
                    isPluginDisabled = false;
                }
            });
        });
    }

    [ConsoleCommand("css_restrictbombsite", "Restrict bombsite for current map")]
    [CommandHelper(1, "<site>", whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void RestrictBombsite_CMD(CCSPlayerController player, CommandInfo info)
    {
        var arg = info.GetArg(1);
        if (!int.TryParse(arg, out int site))
        {
            info.ReplyToCommand($"[Bombsite Restrict] The site must be a number!");
            return;
        }
        if (site < 1 && site > 2)
        {
            info.ReplyToCommand($"[Bombsite Restrict] The site must be a 1 (A) or 2 (B)!");
            return;
        }
        string allowedSite = site == 1 ? "A" : "B";
        info.ReplyToCommand($"[Bombsite Restrict] {allowedSite} plant was blocked on this map. If there are less than {Config.iMinPlayers} players.");
        Config.iDisabledSite = site;
    }

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (isPluginDisabled)
            return HookResult.Continue;

        if (hudTimer != null)
            hudTimer.Kill();

        disabledSite = 0;
        var Sites = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_bomb_target");
        foreach (var entity in Sites)
        {
            if (entity.IsValid)
                entity.AcceptInput("Enable");
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (isPluginDisabled)
            return HookResult.Continue;

        if (!GameRules().WarmupPeriod)
        {
            if (GetPlayersCount() <= Config.iMinPlayers)
            {
                int iSite = Config.iDisabledSite;
                if (Config.iDisabledSite == 0)
                {
                    Random random = new Random();
                    iSite = random.Next(1, 3);
                }
                disabledSite = iSite;
                string site = disabledSite == 1 ? "B" : "A";
                DisableBombsite();

                if (teamMessages == 3 || teamMessages == 2)
                {
                    foreach (var player in Utilities.GetPlayers().Where(p => !p.IsBot && !p.IsHLTV && p.TeamNum == teamMessages))
                    {
                        player.PrintToChat($"{Localizer["Prefix"]} {Localizer["Bombsite_Disabled", site, Config.iMinPlayers]}");
                    }
                }
                else
                {
                    Server.PrintToChatAll($"{Localizer["Prefix"]} {Localizer["Bombsite_Disabled", site, Config.iMinPlayers]}");
                }
                if (Config.iTimer > 1)
                {
                    hudTimer = AddTimer(Config.iTimer, () =>
                    {
                        disabledSite = 0;
                    });
                }
            }
            else
            {
                disabledSite = 0;
            }
        }
        return HookResult.Continue;
    }

    public void DrawLaserBetween(Vector startPos, Vector endPos, string _color = "")
        {
            string beamColor;
            beamColor = _color;
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam")!;
            if (beam == null)
            {
                Console.WriteLine($"Failed to create beam...");
                return;
            }
            beam.Render = Color.FromName(beamColor);
            beam.Width = 1.5f;
            beam.Teleport(startPos, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            beam.EndPos.X = endPos.X;
            beam.EndPos.Y = endPos.Y;
            beam.EndPos.Z = endPos.Z;

            beam.DispatchSpawn();
            Console.WriteLine($"Beam Spawned at S:{startPos} E:{beam.EndPos}");
        }
 public void DrawWireframe3D(Vector corner1, Vector corner8, string _color)
        {
            Vector corner2 = new(corner1.X, corner8.Y, corner1.Z);
            Vector corner3 = new(corner8.X, corner8.Y, corner1.Z);
            Vector corner4 = new(corner8.X, corner1.Y, corner1.Z);

            Vector corner5 = new(corner8.X, corner1.Y, corner8.Z);
            Vector corner6 = new(corner1.X, corner1.Y, corner8.Z);
            Vector corner7 = new(corner1.X, corner8.Y, corner8.Z);

            //top square
            DrawLaserBetween(corner1, corner2, _color);
            DrawLaserBetween(corner2, corner3, _color);
            DrawLaserBetween(corner3, corner4, _color);
            DrawLaserBetween(corner4, corner1, _color);

            //bottom square
            DrawLaserBetween(corner5, corner6, _color);
            DrawLaserBetween(corner6, corner7, _color);
            DrawLaserBetween(corner7, corner8, _color);
            DrawLaserBetween(corner8, corner5, _color);

            //connect them both to build a cube
            DrawLaserBetween(corner1, corner6, _color);
            DrawLaserBetween(corner2, corner7, _color);
            DrawLaserBetween(corner3, corner8, _color);
            DrawLaserBetween(corner4, corner5, _color);
        }

    private void DisableBombsite()
    {
        Vector? startMins = null;
        Vector? startMaxs = null;
        var Sites = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("func_bomb_target");
        foreach (var entity in Sites)
        {
            var site = new CBombTarget(NativeAPI.GetEntityFromIndex((int)entity.Index));
            int entitySite = site.IsBombSiteB ? 2 : 1;
            if (entitySite == disabledSite)
            {
                if (entity.IsValid)
                {
                    entity.AcceptInput("Disable");
                    
                    startMins = new Vector(1106, -414, -100);
                    startMaxs = new Vector(1300, 300, -100);
                    //startMaxs = entity.Collision!.Maxs + entity.CBodyComponent.SceneNode.AbsOrigin;
                    //startMins = entity.Collision!.Mins + entity.CBodyComponent!.SceneNode!.AbsOrigin;
                    //startMaxs = entity.Collision!.Maxs + entity.CBodyComponent.SceneNode.AbsOrigin;
                    DrawWireframe3D(startMins, startMaxs, "#ff0000");

                }

            }
        }
    }
    private int GetPlayersCount()
    {
        var playersList = Utilities.GetPlayers().Where(p => !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected && (p.Team == CsTeam.Terrorist || p.Team == CsTeam.CounterTerrorist)).ToList();

        if (!Config.bCountBots)
            playersList.RemoveAll(p => p.IsBot);

        if (Config.iTeam == 1 || Config.iTeam == 2)
            playersList.RemoveAll(p => p.TeamNum != Config.iTeam + 1);

        Server.PrintToChatAll($"{playersList.Count()}");
        return playersList.Count();
    }
    internal static CCSGameRules GameRules()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
    }
}
