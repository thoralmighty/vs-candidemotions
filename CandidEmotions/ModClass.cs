using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        /// <summary>
        /// Code to run when the mod is started.
        /// </summary>
        /// <param name="api">API.</param>
        public override void StartServerSide(ICoreServerAPI api)
        {
            config = Utils.GetConfig(api, "CandidEmotionsConfig");

            EnumChatType announceType = config.GetAnnounceType();

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
                    action = AutocompleteOrAutocorrectPhrase(api, config, nearestPlayer, allPlayers, action);
                }
                catch (NoPlayerNearbyException)
                {
                    api.SendMessage(player, groupId, "There is no one here right now", EnumChatType.Notification);
                    return;
                }

                api.SendMessageToGroup(groupId, " * " + player.PlayerName + " " + action, config.GetAnnounceType());
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
                    message = AutocompleteOrAutocorrectPhrase(api, nearestPlayer, allPlayers, message);
                }
                catch (NoPlayerNearbyException)
                {
                    api.SendMessage(player, groupId, "There is no one here right now", EnumChatType.Notification);
                    return;
                }

                api.SendMessageToGroup(groupId, " * " + message, config.GetAnnounceType());
            };
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

            if (allPlayers == null) return word;

            try
            {
                IPlayer playerMatch = null;
                IPlayer approxPlayerMatch = null;

                //find a player with either a matching username or roughly matching by a certain %
                if (config.autocorrect == true)
                {
                    approxPlayerMatch = allPlayers.FirstOrDefault(p =>
                    {
                        return (p.PlayerName.ToLower() == word.ToLower()
                            || Utils.IsFuzzyMatch(p.PlayerName.ToLower(), word.ToLower(), config.autocompleteThreshold));
                    });
                }

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

                if (word == "@p")
                {
                    if (nearestPlayer == null)
                        throw new NoPlayerNearbyException();
                    word = nearestPlayer.PlayerName;
                }
                else if (config.autocomplete && playerMatch != null)
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
                if (throwExceptions) throw;
                api.Logger.Error(string.Format("Unable to autocomplete/autocorrect word \"{0}\": {1}", originalWord, ex));
            }

            return word;
        }
    }
}
