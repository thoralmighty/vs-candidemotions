using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

//Required for debugging when building directly to Mods
[assembly: ModInfo("CandidEmotions")]

namespace CandidEmotions
{
    public class CandidEmotionsMod : ModSystem
    {
        CandidEmotionsConfig config;
        AutoCorrector corrector;

        #region Initialization
        public override void Start(ICoreAPI api)
        {
            config = Utils.GetConfig(api, "CandidEmotionsConfig");
            corrector = new AutoCorrector(config);
        }

        /// <summary>
        /// Code to run when the mod is started.
        /// </summary>
        /// <param name="api">API.</param>
        public override void StartServerSide(ICoreServerAPI api)
        {
            EnumChatType announceType = config.GetChatEnumType();

            SetupCoreEmotes(api);
            SetupPoint(api);
        }

        private void SetupPoint(ICoreServerAPI api)
        {
            ServerChatCommand point = new ServerChatCommand()
            {
                Command = "point",
                Description = "Points at an object or point ahead"
            };

            point.handler = (IServerPlayer player, int groupId, CmdArgs args) => {
                String message = "";

                corrector.Refresh(api);

                if (player.CurrentEntitySelection != null)
                {
                    IPlayer targetPlayer = api.World.AllOnlinePlayers.FirstOrDefault(p => p.Entity.EntityId == player.CurrentEntitySelection.Entity.EntityId);
                    bool isPlayer = targetPlayer != null;

                    if (isPlayer)
                    {
                        message = string.Format("{0} points at {1}", player.PlayerName, targetPlayer.PlayerName);
                    }
                    else
                    {
                        string entityName = player.CurrentEntitySelection.Entity.GetName().ToLower();
                        message = string.Format("{0} points at {1}", player.PlayerName, entityName);

                        if (player.CurrentEntitySelection.Entity.Pos.DistanceTo(player.Entity.Pos.XYZ) > 50)
                        {
                            message += " in the distance";
                        }
                        else if (player.CurrentEntitySelection.Entity.Pos.DistanceTo(player.Entity.Pos.XYZ) > 20)
                        {
                            message += " nearby";
                        }
                    }
                }
                else if (player.CurrentBlockSelection != null)
                {
                    Block block = api.World.BlockAccessor.GetBlockOrNull(player.CurrentBlockSelection.Position.X, player.CurrentBlockSelection.Position.Y, player.CurrentBlockSelection.Position.Z);
                    if (block == null)
                    {
                        message = string.Format("{0} points in front of them", player.PlayerName);
                    }
                    else
                    {
                        StringBuilder stringBuilder = new StringBuilder();

                        stringBuilder.Append(string.Format("{0} points at ", player.PlayerName));

                        EnumBlockMaterial[] ignoreMaterials = new EnumBlockMaterial[]
                        {
                            EnumBlockMaterial.Air,
                            EnumBlockMaterial.Gravel,
                            EnumBlockMaterial.Leaves,
                            EnumBlockMaterial.Ice,
                            EnumBlockMaterial.Sand,
                            EnumBlockMaterial.Snow, 
                            EnumBlockMaterial.Soil,
                            EnumBlockMaterial.Stone,
                            EnumBlockMaterial.Wood
                        };

                        if (!ignoreMaterials.Contains(block.BlockMaterial))
                        {
                            stringBuilder.Append(block.GetPlacedBlockName(api.World, player.CurrentBlockSelection.Position).ToLower());
                        }
                        else
                        {
                            stringBuilder.Append("the ground");
                        }

                        var distance = player.CurrentBlockSelection.Position.DistanceTo(player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z);

                        if (distance > 50)
                        {
                            stringBuilder.Append(" in the distance");
                        }
                        else if (distance > 20)
                        {
                            stringBuilder.Append(" nearby");
                        }

                        message = stringBuilder.ToString();
                    }
                }
                else
                {
                    return;
                }

                api.SendMessage(player, groupId, config.GetPrefix() + message, config.GetChatEnumType());
            };

            api.RegisterCommand(point);
        }
        #endregion

        private void SetupCoreEmotes(ICoreServerAPI api)
        {
            List<string> allActions = new List<string>();

            if (config.enableHugs) allActions.Add("hug");
            if (config.customActions != null) allActions.AddRange(config.customActions);

            //Add custom actions as commands with respective handlers
            foreach (string action in allActions.Distinct())
            {
                ServerChatCommand cmd = new ServerChatCommand()
                {
                    Command = action,
                    Description = string.Format("Virtually {0}s another player", action),
                    Syntax = action + " [player]"
                };

                cmd.handler = GetHandlerDelegate(api, action);
                api.RegisterCommand(cmd);
            }

            //Add the /me command and handler
            ServerChatCommand me = new ServerChatCommand()
            {
                Command = "me",
                Description = "Virtually performs an action",
                Syntax = "me [action]"
            };

            me.handler = GetMeHandler(api);
            api.RegisterCommand(me);
        }

        private ServerChatCommandDelegate GetMeHandler(ICoreServerAPI api)
        {
            return delegate (IServerPlayer player, int groupId, CmdArgs args) {
                string action = args.PopAll().Trim();

                corrector.Refresh(api);

                if (action.Length == 0)
                {
                    api.SendMessage(player, groupId, "Need to provide an action", EnumChatType.Notification);
                    return;
                }

                //Replace placeholder and autocorrect if applicable
                IPlayer nearestPlayer = Utils.FindNearestPlayer(api, config, player);
                IPlayer[] allPlayers = api.World.AllPlayers;
                string[] words = action.Split(new char[] { ' ' });

                try
                { 
                    action = RequestAutoCorrection(action, nearestPlayer);
                    api.SendMessageToGroup(groupId, config.GetPrefix() + player.PlayerName + " " + action, config.GetChatEnumType());
                }
                catch (NoPlayerNearbyException)
                {
                    api.SendMessage(player, groupId, "There is no one here right now", EnumChatType.Notification);
                }
            };
        }

        private void Error(ICoreAPI api, string v)
        {
            api.Logger.Error("(CandidEmotions) " + v);
        }

        private ServerChatCommandDelegate GetHandlerDelegate(ICoreServerAPI api, string action)
        {
            return delegate (IServerPlayer player, int groupId, CmdArgs args) {
                string otherPlayerName = args.PopAll().Trim();
                string message = "";
                IPlayer nearestPlayer = Utils.FindNearestPlayer(api, config, player);
                IPlayer[] allPlayers = api.World.AllPlayers;

                corrector.Refresh(api);

                //if the specified user to perform the action on is not set or is set to the placeholder, get nearest player
                if (otherPlayerName.Length == 0 || otherPlayerName == config.placeholder)
                {
                    //if there is another player nearby
                    if (nearestPlayer != null && nearestPlayer.PlayerName != player.PlayerName)
                    {
                        message = string.Format("{0} {1}s {2}", player.PlayerName, action, nearestPlayer.PlayerName);
                    }
                    else
                    {
                        //if there are no other players nearby
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
                    //perform action on specified player
                    message = string.Format("{0} hugs {1}", player.PlayerName, args.PopAll());
                }

                try
                {
                    message = RequestAutoCorrection(message, nearestPlayer);
                    api.SendMessageToGroup(groupId, config.GetPrefix() + message, config.GetChatEnumType());
                }
                catch (NoPlayerNearbyException)
                {
                    api.SendMessage(player, groupId, "There is no one here right now", EnumChatType.Notification);
                }
            };
        }

        private string RequestAutoCorrection(string input, IPlayer nearestPlayer)
        {
            input = Utils.ReplacePlaceholder(input, config.placeholder, nearestPlayer);
            input = corrector.Process(input, nearestPlayer);
            return input;
        }
    }
}
