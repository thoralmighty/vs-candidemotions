using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CandidEmotions
{
    public class CandidEmotionsMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
        }

        /// <summary>
        /// Code to run when the mod is started.
        /// </summary>
        /// <param name="api">API.</param>
        public override void StartServerSide(ICoreServerAPI api)
        {
            CandidEmotionsConfig config = GetConfig(api);

            EnumChatType announceType = config.announceAsMessage == true ? EnumChatType.OthersMessage : EnumChatType.Notification;

            List<string> allActions = new List<string>();

            if (config.enableHugs) allActions.Add("hug");
            if (config.customActions != null) allActions.AddRange(config.customActions);

            /*
             * Add custom actions as commands with respective handlers
             */
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
                    IPlayer nearestPlayer = FindNearestPlayer(config, api, player);
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
                        message = AutocompleteOrAutocorrectPhrase(api, config, nearestPlayer, allPlayers, message);
                    }
                    catch (NoPlayerNearbyException)
                    {
                        api.SendMessage(player, groupId, "There is no one here right now", EnumChatType.Notification);
                        return;
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

                //Replace placeholder and autocorrect if applicable
                IPlayer nearestPlayer = FindNearestPlayer(config, api, player);
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

                api.SendMessageToGroup(groupId, " * " + player.PlayerName + " " + action, announceType);
            };

            api.RegisterCommand(me);
        }

        /// <summary>
        /// Attempts to autocomplete or autocorrect player names in a phrase.
        /// </summary>
        /// <returns>The corrected phrase, or as-is if an error occurred.<</returns>
        /// <param name="api">API.</param>
        /// <param name="config">Config.</param>
        /// <param name="nearestPlayer">Nearest player.</param>
        /// <param name="allPlayers">All players.</param>
        /// <param name="fullPhrase">Full phrase to work with.</param>
        private string AutocompleteOrAutocorrectPhrase(ICoreServerAPI api, CandidEmotionsConfig config, IPlayer nearestPlayer, IEnumerable<IPlayer> allPlayers, string fullPhrase)
        {
            try
            {
                string[] words = fullPhrase.Split(new char[] { ' ' });
                for (int i = 0; i < words.Length; i++)
                {
                    words[i] = AutocompleteOrAutocorrect(api, config, nearestPlayer, allPlayers, words[i], true);
                }
                return string.Join(" ", words);
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
        /// <param name="config">Config.</param>
        /// <param name="nearestPlayer">Nearest player.</param>
        /// <param name="allPlayers">All players.</param>
        /// <param name="word">Single word to work with.</param>
        private string AutocompleteOrAutocorrect(ICoreServerAPI api, CandidEmotionsConfig config, IPlayer nearestPlayer, IEnumerable<IPlayer> allPlayers, string word, bool throwExceptions)
        {
            string originalWord = word;

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
                            || IsFuzzyMatch(config, p.PlayerName.ToLower(), word.ToLower()));
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
            catch (Exception ex)
            {
                if (throwExceptions) throw;
                api.Logger.Error(string.Format("Unable to autocomplete/autocorrect word \"{0}\": {1}", originalWord, ex));
            }

            return word;
        }

        /// <summary>
        /// Checks if one word is almost similar to another (e.g. with a typo).
        /// </summary>
        /// <returns><c>true</c>, if fuzzy match was found, <c>false</c> otherwise.</returns>
        /// <param name="config">Config.</param>
        /// <param name="target">Target word.</param>
        /// <param name="sample">Sample word (user input).</param>
        private bool IsFuzzyMatch(CandidEmotionsConfig config, string target, string sample)
        {
            sample = sample.Trim();
            target = target.Trim();

            //if the sample is the same length as the target or varies by max 2 characters
            if (target.Length == sample.Length || (Math.Max(target.Length, sample.Length) - Math.Min(target.Length, sample.Length)) <= 2)
            {
                char[] target_c = target.ToArray();
                char[] sample_c = sample.ToArray();

                int match = 0;
                for (int i = 0; i < Math.Min(target_c.Length, sample_c.Length); i++)
                {
                    if (target_c[i] == sample_c[i])
                        match++;
                }

                return (match >= target.Length * config.autocompleteThreshold);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the nearest player.
        /// </summary>
        /// <returns>The nearest player.</returns>
        /// <param name="config">Config.</param>
        /// <param name="api">API.</param>
        /// <param name="player">Player.</param>
        private IPlayer FindNearestPlayer(CandidEmotionsConfig config, ICoreServerAPI api, IServerPlayer player)
        {
            var playerPos = player.Entity.ServerPos;
            IPlayer nearbyPlayer = api.World.GetPlayersAround(player.Entity.ServerPos.XYZ, config.playerSearchRadius, config.playerSearchRadius, p => p.PlayerName != player.PlayerName)
                .OrderBy(p => p.Entity.ServerPos.DistanceTo(playerPos.XYZ))
                .FirstOrDefault();
            return nearbyPlayer;
        }

        /// <summary>
        /// Gets the config if one is available, otherwise default.
        /// </summary>
        /// <returns>The config.</returns>
        /// <param name="api">API.</param>
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

    /// <summary>
    /// Exception to be thrown when no player was found nearby.
    /// </summary>
    [Serializable]
    internal class NoPlayerNearbyException : Exception
    {
        public NoPlayerNearbyException()
        {
        }

        public NoPlayerNearbyException(string message) : base(message)
        {
        }

        public NoPlayerNearbyException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected NoPlayerNearbyException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
