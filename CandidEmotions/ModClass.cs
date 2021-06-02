using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

[assembly: ModInfo("Candid Emotions",
    Description = "Allows players to perform actions and express themselves in chat",
    Website = "https://github.com/thoralmighty",
    Authors = new[] { "thoralmighty" },
    Version = "1.1.0")]

namespace CandidEmotions
{
    public class CandidEmotionsMod : ModSystem
    {
        public CandidEmotionsMod()
        {
        }

        public override void Start(ICoreAPI api)
        {
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            CandidEmotionsConfig config = GetConfig(api);

            EnumChatType announceType = config.announceAsMessage == true ? EnumChatType.OthersMessage : EnumChatType.Notification;

            List<string> allActions = new List<string>();

            if (config.enableHugs) allActions.Add("hug");
            if (config.customActions != null) allActions.AddRange(config.customActions);

            foreach (string action in allActions.Distinct())
            {
                ServerChatCommand cmd = new ServerChatCommand()
                {
                    Command = action,
                    Description = string.Format("Virtually {0}s another player", action),
                    Syntax = action + " [player]"
                };

                cmd.handler = delegate (IServerPlayer player, int groupId, CmdArgs args) {
                    string otherPlayerName = args.PopAll().Trim();
                    string message = "";

                    if (otherPlayerName.Length == 0 || otherPlayerName == "@p")
                    {
                        IPlayer nearestPlayer = FindNearestPlayer(api, player);
                        if (nearestPlayer != null && nearestPlayer.PlayerName != player.PlayerName)
                        {
                            message = string.Format("{0} {1}s {2}", player.PlayerName, action, nearestPlayer.PlayerName);
                        }
                        else
                        {
                            if (action == "hug")
                            {
                                api.SendMessage(player, groupId, "There is no one around, so you give yourself a hug", EnumChatType.Notification);
                            }
                            else
                            {
                                api.SendMessage(player, groupId, string.Format("There is no one around, so you {0} yourself", action), EnumChatType.Notification);
                            }
                            return;
                        }
                    }
                    else
                    {
                        message = string.Format("{0} hugs {1}", player.PlayerName, args.PopAll());
                    }

                    api.SendMessageToGroup(groupId, " * " + message, announceType);
                };

                api.RegisterCommand(cmd);
            }

            /*
             * Add the /me command and handler
             */
            ServerChatCommand me = new ServerChatCommand()
            {
                Command = "me",
                Description = "Virtually performs an action",
                Syntax = "me [action]"
            };

            me.handler = delegate (IServerPlayer player, int groupId, CmdArgs args) {
                string action = args.PopAll().Trim();

                if (action.Length == 0)
                {
                    api.SendMessage(player, groupId, "Need to provide an action", EnumChatType.Notification);
                    return;
                }

                //Replace placeholder if applicable
                if (action.Contains("@p"))
                {
                    string[] words = action.Split(new char[] { ' ' });
                    IPlayer nearestPlayer = FindNearestPlayer(api, player);

                    if (nearestPlayer == null || nearestPlayer.PlayerName == player.PlayerName)
                    {
                        api.SendMessage(player, groupId, "There is no one here right now", EnumChatType.Notification);
                        return;
                    }

                    for (int i = 0; i < words.Length; i++)
                    {
                        if (words[i] == "@p")
                        {
                            words[i] = nearestPlayer.PlayerName;
                        }
                    }

                    action = string.Join(" ", words);
                }

                api.SendMessageToGroup(groupId, " * " + player.PlayerName + " " + action, announceType);
            };

            api.RegisterCommand(me);
        }

        private IPlayer FindNearestPlayer(ICoreServerAPI api, IServerPlayer player)
        {
            var playerPos = player.Entity.ServerPos;
            IPlayer nearbyPlayer = api.World.GetPlayersAround(player.Entity.ServerPos.XYZ, 30f, 30f, p => p.PlayerName != player.PlayerName)
                .OrderBy(p => p.Entity.ServerPos.DistanceTo(playerPos.XYZ))
                .FirstOrDefault();
            return nearbyPlayer;
        }

        private CandidEmotionsConfig GetConfig(ICoreServerAPI api)
        {
            try
            {
                CandidEmotionsConfig cfg = api.LoadModConfig<CandidEmotionsConfig>("CandidEmotionsConfig");
                if (cfg == null) return new CandidEmotionsConfig();
                else return cfg;
            }
            catch (Exception)
            {
                return new CandidEmotionsConfig(); //default
            }
        }
    }
}
