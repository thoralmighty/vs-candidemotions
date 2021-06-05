using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CandidEmotions
{
    internal class AutoCorrector
    {
        CandidEmotionsConfig config;
        IEnumerable<IPlayer> playerList;

        public AutoCorrector(CandidEmotionsConfig config)
        {
            this.config = config;
        }

        internal void Refresh(ICoreServerAPI api)
        {
            playerList = api.World.AllPlayers;
        }

        /// <summary>
        /// Attempts to autocomplete or autocorrect player names in a phrase.
        /// </summary>
        /// <returns>The corrected phrase, or as-is if an error occurred.</returns>
        /// <param name="nearestPlayer">Nearest player.</param>
        /// <param name="fullPhrase">Full phrase to work with.</param>
        internal string Process(string fullPhrase, IPlayer nearestPlayer)
        {
            try
            {
                string[] words = fullPhrase.Split(new char[] { ' ' });
                for (int i = 0; i < words.Length; i++)
                {
                    words[i] = ProcessWord(words[i], nearestPlayer, true);
                }
                return string.Join(" ", words);
            }
            catch (NoPlayerNearbyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                //api.Logger.Error(string.Format("Unable to autocomplete/autocorrect phrase \"{0}\": {1}", fullPhrase, ex));
                throw new Exception("Unable to autocorrect phrase: " + ex);
            }
        }

        /// <summary>
        /// Attempts to autocomplete or autocorrect a player name in a word.
        /// </summary>
        /// <returns>The corrected word, or as-is if an error occurred.</returns>
        /// <param name="nearestPlayer">Nearest player.</param>
        /// <param name="word">Single word to work with.</param>
        private string ProcessWord(string word, IPlayer nearestPlayer, bool throwExceptions = false)
        {
            string originalWord = word;

            if (playerList == null || word == null) return word;

            //failsafe null check
            playerList = playerList.Where(p => p != null);

            IPlayer playerMatch = null;
            IPlayer approxPlayerMatch = null;
            //find a player whose name starts with what the user entered
            if (config.autocomplete == true)
            {
                playerMatch = playerList.FirstOrDefault(p =>
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
                approxPlayerMatch = playerList.FirstOrDefault(p =>
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

            return word;
        }
    }
}