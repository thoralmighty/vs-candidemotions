using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace CandidEmotions
{
    class Utils
    {
        /// <summary>
        /// Finds the nearest player.
        /// </summary>
        /// <returns>The nearest player.</returns>
        /// <param name="config">Config.</param>
        /// <param name="api">API.</param>
        /// <param name="player">Player.</param>
        public static IPlayer FindNearestPlayer(ICoreServerAPI api, CandidEmotionsConfig config, IServerPlayer player)
        {
            var playerPos = player.Entity.ServerPos;
            IPlayer nearbyPlayer = api.World.GetPlayersAround(player.Entity.Pos.XYZ, config.playerSearchRadius, config.playerSearchRadius, p => p.PlayerName != player.PlayerName)
                .OrderBy(p => p.Entity.ServerPos.DistanceTo(playerPos.XYZ))
                .FirstOrDefault();
            return nearbyPlayer;
        }

        /// <summary>
        /// Gets the config if one is available, otherwise default.
        /// </summary>
        /// <returns>The config.</returns>
        /// <param name="api">API.</param>
        public static CandidEmotionsConfig GetConfig(ICoreAPI api, string configName)
        {
            try
            {
                CandidEmotionsConfig cfg = api.LoadModConfig<CandidEmotionsConfig>(configName);
                if (cfg == null) return new CandidEmotionsConfig();
                return cfg;
            }
            catch (Exception)
            {
                return new CandidEmotionsConfig(); //default
            }
        }

        /// <summary>
        /// Checks if one word is almost similar to another (e.g. with a typo).
        /// </summary>
        /// <returns><c>true</c>, if fuzzy match was found, <c>false</c> otherwise.</returns>
        /// <param name="target">Target word.</param>
        /// <param name="sample">Sample word (user input).</param>
        public static bool IsFuzzyMatch(string target, string sample, double autocompleteThreshold)
        {
            if (target == sample)
                return true;

            if (target == null || sample == null)
                return false;

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

                return (match >= target.Length * autocompleteThreshold);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Replaces the placeholder defined in <see cref="CandidEmotionsConfig"/> with the nearest player name.
        /// </summary>
        /// <returns>The placeholder.</returns>
        /// <param name="message">Message.</param>
        /// <param name="nearestPlayer">Nearest player.</param>
        public static string ReplacePlaceholder(string message, string placeholder, IPlayer nearestPlayer)
        {
            string[] words = message.Split(new char[] { ' ' });

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i]?.Trim() == placeholder)
                {
                    if (nearestPlayer != null)
                        words[i] = nearestPlayer.PlayerName;
                    else
                        throw new NoPlayerNearbyException();
                }
            }

            return string.Join(" ", words);
        }
    }
}