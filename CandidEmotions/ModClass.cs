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
    Version = "1.0.1")]

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
                        IPlayer nearestPlayer = api.World.NearestPlayer(player.Entity.ServerPos.X, player.Entity.ServerPos.Y, player.Entity.ServerPos.Z);
                        if (nearestPlayer != null && nearestPlayer.PlayerName != player.PlayerName)
                        {
                            message = string.Format("{0} {1}s {2}", player.PlayerName, action, nearestPlayer.PlayerName);
                        }
                        else
                        {
                            api.SendMessage(player, groupId, "There was no one around, so you gave yourself a hug", EnumChatType.Notification);
                            return;
                        }
                    }
                    else
                    {
                        message = string.Format("{0} hugs {1}", player.PlayerName, args.PopAll());
                    }

                    api.SendMessageToGroup(groupId, " * " + message, EnumChatType.Notification);
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
                    IPlayer nearestPlayer = api.World.NearestPlayer(player.Entity.ServerPos.X, player.Entity.ServerPos.Y, player.Entity.ServerPos.Z);

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

                api.SendMessageToGroup(groupId, " * " + player.PlayerName + " " + action, EnumChatType.Notification);
            };

            api.RegisterCommand(me);
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
