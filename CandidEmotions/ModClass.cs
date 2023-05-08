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
            SetupCoreEmotes(api);
            SetupPoint(api);
        }

        private void SetupPoint(ICoreServerAPI api)
        {
            var point = api.ChatCommands.Create()
                .WithName("point")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .WithDescription("Points at an object or point ahead")
                .HandleWith((TextCommandCallingArgs args) =>
                {
                    string message = "";
                    var player = args.Caller.Player;
                    var groupId = args.Caller.FromChatGroupId;

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

                            if (player.CurrentEntitySelection.Entity.Pos.DistanceTo(player.Entity.Pos.XYZ) > 20)
                            {
                                message += " in the distance";
                            }
                            else if (player.CurrentEntitySelection.Entity.Pos.DistanceTo(player.Entity.Pos.XYZ) > 10)
                            {
                                message += " nearby";
                            }
                        }
                    }
                    else if (player.CurrentBlockSelection != null)
                    {
                        Block block = api.World.BlockAccessor.GetBlockOrNull(player.CurrentBlockSelection.Position.X, player.CurrentBlockSelection.Position.Y, player.CurrentBlockSelection.Position.Z);
                        BlockEntity blockEntity = api.World.BlockAccessor.GetBlockEntity(player.CurrentBlockSelection.Position);

                        if (block == null && blockEntity == null)
                        {
                            //neither a block or an entity
                            message = string.Format("{0} points in front of them", player.PlayerName);
                        }
                        else if (blockEntity != null)
                        {
                            //block entity (chest, cooking pot etc)
                            message = string.Format("{0} points at the {1}", player.PlayerName, blockEntity.Block.GetPlacedBlockName(api.World, player.CurrentBlockSelection.Position).ToLower());
                        }
                        else if (block != null)
                        {
                            //normal block
                            StringBuilder stringBuilder = new StringBuilder();

                            stringBuilder.Append(string.Format("{0} points ", player.PlayerName));

                            EnumBlockMaterial[] groundMaterials = new EnumBlockMaterial[]
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

                            if (!groundMaterials.Contains(block.BlockMaterial))
                            {
                                stringBuilder.Append("at " + block.GetPlacedBlockName(api.World, player.CurrentBlockSelection.Position).ToLower());
                            }
                            else if (block.BlockMaterial == EnumBlockMaterial.Air)
                            {
                                stringBuilder.Append("into thin air");
                            }
                            else
                            {
                                stringBuilder.Append("at the ground");
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
                        return TextCommandResult.Error("There seems to be nothing there");
                    }

                    api.SendMessage(player, groupId, config.GetPrefix() + message, config.GetChatEnumType());
                    return TextCommandResult.Success();
                });
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
                api.ChatCommands.Create()
                    .WithName(action)
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalPlayerUids("player"))
                    .WithDescription(string.Format("Virtually {0}{1} another player", action, action.EndsWith("s") ? "" : "s"))
                    .HandleWith(GetHandlerDelegate(api, action));
            }

            //Add the /me command and handler
            api.ChatCommands.Create()
                .WithName("me")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(api.ChatCommands.Parsers.All("action"))
                .RequiresPlayer()
                .WithDescription("Virtually performs an action")
                .WithExamples(new string[] { "/me ponders" })
                .HandleWith(GetMeHandler(api));
        }

        private OnCommandDelegate GetMeHandler(ICoreServerAPI api)
        {
            return delegate (TextCommandCallingArgs args) {
                string action = (string)args.Parsers[0].GetValue();
                var player = args.Caller.Player;
                var groupId = args.Caller.FromChatGroupId;


                corrector.Refresh(api);

                if (action.Length == 0)
                {
                    return TextCommandResult.Error("Need to provide an action");
                }

                //Replace placeholder and autocorrect if applicable
                IPlayer nearestPlayer = Utils.FindNearestPlayer(api, config, player);
                IPlayer[] allPlayers = api.World.AllPlayers;
                string[] words = action.Split(new char[] { ' ' });

                try
                { 
                    action = RequestAutoCorrection(action, nearestPlayer);
                    api.SendMessageToGroup(groupId, config.GetPrefix() + player.PlayerName + " " + action, config.GetChatEnumType());
                    return TextCommandResult.Success();
                }
                catch (NoPlayerNearbyException)
                {
                    return TextCommandResult.Error("There is no one here right now");
                }
            };
        }

        private void Error(ICoreAPI api, string v)
        {
            api.Logger.Error("(CandidEmotions) " + v);
        }

        private OnCommandDelegate GetHandlerDelegate(ICoreServerAPI api, string action)
        {
            return delegate (TextCommandCallingArgs args) {
                IPlayer player = args.Caller.Player;
                int groupId = args.Caller.FromChatGroupId;
                string otherPlayerName = "";

                if (!args.Parsers[0].IsMissing)
                {
                    if (args.Parsers[0].GetValue() is PlayerUidName)
                    {
                        otherPlayerName = ((PlayerUidName)args.Parsers[0].GetValue()).Name;
                    }
                    else if (args.Parsers[0].GetValue() is PlayerUidName[])
                    {
                        otherPlayerName = string.Join(", ", ((PlayerUidName[])args.Parsers[0].GetValue()).Select(p => p.Name));
                    }
                }
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
                        return TextCommandResult.Success();
                    }
                }
                else
                {
                    //perform action on specified player
                    message = string.Format("{0} hugs {1}", player.PlayerName, otherPlayerName);
                }

                try
                {
                    message = RequestAutoCorrection(message, nearestPlayer);
                    api.SendMessageToGroup(groupId, config.GetPrefix() + message, config.GetChatEnumType());
                    return TextCommandResult.Success();
                }
                catch (NoPlayerNearbyException)
                {
                    return TextCommandResult.Error("There is no one here right now");
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
