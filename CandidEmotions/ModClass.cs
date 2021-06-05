using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

//Required for debugging when building directly to Mods
[assembly: ModInfo("CandidEmotions")]

namespace CandidEmotions
{
    public class CandidEmotionsMod : ModSystem
    {
        CandidEmotionsConfig config;

        public override void Start(ICoreAPI api)
        {
            config = Utils.GetConfig(api, "CandidEmotionsConfig");
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

            api.RegisterCommand(new ServerChatCommand()
            {
                Command = "emote",
                handler = (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    api.SendMessage(player, groupId, "Oh, it worked", EnumChatType.CommandSuccess);
                }
            });
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
                    action = ReplacePlaceholder(action, nearestPlayer);
                    action = AutocompleteOrAutocorrectPhrase(api, nearestPlayer, allPlayers, action);
                }
                catch (NoPlayerNearbyException)
                {
                    api.SendMessage(player, groupId, "There is no one here right now", EnumChatType.Notification);
                    return;
                }

                api.SendMessageToGroup(groupId, config.GetPrefix() + player.PlayerName + " " + action, config.GetChatEnumType());
            };
        }

        private ServerChatCommandDelegate GetHandlerDelegate(ICoreServerAPI api, string action)
        {
            return delegate (IServerPlayer player, int groupId, CmdArgs args) {
                string otherPlayerName = args.PopAll().Trim();
                string message = "";
                IPlayer nearestPlayer = Utils.FindNearestPlayer(api, config, player);
                IPlayer[] allPlayers = api.World.AllPlayers;

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
                    message = ReplacePlaceholder(message, nearestPlayer);
                    message = AutocompleteOrAutocorrectPhrase(api, nearestPlayer, allPlayers, message);
                }
                catch (NoPlayerNearbyException)
                {
                    api.SendMessage(player, groupId, "There is no one here right now", EnumChatType.Notification);
                    return;
                }

                api.SendMessageToGroup(groupId, config.GetPrefix() + message, config.GetChatEnumType());
            };
        }

        /// <summary>
        /// Replaces the placeholder defined in <see cref="CandidEmotionsConfig"/> with the nearest player name.
        /// </summary>
        /// <returns>The placeholder.</returns>
        /// <param name="message">Message.</param>
        /// <param name="nearestPlayer">Nearest player.</param>
        private string ReplacePlaceholder(string message, IPlayer nearestPlayer)
        {
            string[] words = message.Split(new char[] { ' ' });

            for(int i = 0; i < words.Length; i++)
            {
                if (words[i] == config.placeholder)
                {
                    if (nearestPlayer != null)
                        words[i] = nearestPlayer.PlayerName;
                    else
                        throw new NoPlayerNearbyException();
                }
            }

            return string.Join(" ", words);
        }

        /// <summary>
        /// Attempts to autocomplete or autocorrect player names in a phrase.
        /// </summary>
        /// <returns>The corrected phrase, or as-is if an error occurred.</returns>
        /// <param name="api">API.</param>
        /// <param name="nearestPlayer">Nearest player.</param>
        /// <param name="allPlayers">All players.</param>
        /// <param name="fullPhrase">Full phrase to work with.</param>
        private string AutocompleteOrAutocorrectPhrase(ICoreServerAPI api, IPlayer nearestPlayer, IEnumerable<IPlayer> allPlayers, string fullPhrase)
        {
            try
            {
                string[] words = fullPhrase.Split(new char[] { ' ' });
                for (int i = 0; i < words.Length; i++)
                {
                    words[i] = AutocompleteOrAutocorrect(api, nearestPlayer, allPlayers, words[i], true);
                }
                return string.Join(" ", words);
            }
            catch (NoPlayerNearbyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                api.Logger.Error(string.Format("Unable to autocomplete/autocorrect phrase \"{0}\": {1}", fullPhrase, ex));
                return fullPhrase;
            }
        }

        /// <summary>
        /// Attempts to autocomplete or autocorrect a player name in a word.
        /// </summary>
        /// <returns>The corrected word, or as-is if an error occurred.</returns>
        /// <param name="api">API.</param>
        /// <param name="nearestPlayer">Nearest player.</param>
        /// <param name="allPlayers">All players.</param>
        /// <param name="word">Single word to work with.</param>
        private string AutocompleteOrAutocorrect(ICoreServerAPI api, IPlayer nearestPlayer, IEnumerable<IPlayer> allPlayers, string word, bool throwExceptions)
        {
            string originalWord = word;

            if (allPlayers == null || word == null) return word;

            //failsafe null check
            allPlayers = allPlayers.Where(p => p != null);

            try
            {
                IPlayer playerMatch = null;
                IPlayer approxPlayerMatch = null;
                //find a player whose name starts with what the user entered
                if (config.autocomplete == true)
                {
                    playerMatch = allPlayers.FirstOrDefault(p =>
                    {
                        //ignore too short queries, like "he" for "helloworld"
                        if (word.Length <= config.minimumCompleteLength && p.PlayerName.Length >= config.minimumCompleteLength)
                            return false;
                        return p.PlayerName.ToLower().StartsWith(word.ToLower(), StringComparison.CurrentCulture);
                    });
                }

                //find a player with either a matching username or roughly matching by a certain %
                if (config.autocorrect == true)
                {
                    approxPlayerMatch = allPlayers.FirstOrDefault(p =>
                    {
                        return (p.PlayerName.ToLower() == word.ToLower()
                            || Utils.IsFuzzyMatch(p.PlayerName.ToLower(), word.ToLower(), config.autocompleteThreshold));
                    });
                }
                    
                if (config.autocomplete && playerMatch != null)
                {
                    word = playerMatch.PlayerName;
                }
                else if (config.autocorrect && approxPlayerMatch != null)
                {
                    word = approxPlayerMatch.PlayerName;
                }
            }
            catch (NoPlayerNearbyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (throwExceptions) throw; //skip the following log output and defer to catch in AutocompleteOrAutocorrectPhrase
                api.Logger.Error(string.Format("Unable to autocomplete/autocorrect word \"{0}\": {1}", originalWord, ex));
            }

            return word;
        }
    }
}
