using System;
using Vintagestory.API.Common;

namespace CandidEmotions
{
    /// <summary>
    /// CandidEmotions configuration with defaults.
    /// </summary>
    internal class CandidEmotionsConfig
    {
        /// <summary>
        /// Whether to enable the built-in "hug" emote.
        /// </summary>
        public bool enableHugs = true;

        /// <summary>
        /// Whether to enable the placeholder (default @p, defined by <see cref="enablePlaceholder"/>) which will be replaced with the name of the closest player.
        /// </summary>
        public bool enablePlaceholder = true;

        /// <summary>
        /// Toggle for announcing the emotes as messages in chat rather than notifications. Intended for testing, might have no practical effect.
        /// </summary>
        public bool announceAsMessage = false;

        /// <summary>
        /// Placeholder found in user input that will be replaced with the name of the closest player.
        /// </summary>
        public string placeholder = "@p";

        /// <summary>
        /// Custom actions/emotes (in infinitive form e.g. 'walk') that will be generated as commands by the mod.
        /// </summary>
        public string[] customActions = { };

        /// <summary>
        /// The player search radius when looking for a nearby player. Any player outside of this radius will not be considered.
        /// </summary>
        public float playerSearchRadius = 30;

        /// <summary>
        /// Toggles autocompletion of player names, for example if the user wrote 'Play' and there is a player called 'Player'.
        /// </summary>
        public bool autocomplete = true;

        /// <summary>
        /// Toggles autocorrection of player names, for example if the user wrote 'Plyaer' and there is a player called 'Player'.
        /// </summary>
        public bool autocorrect = true;

        /// <summary>
        /// The minimum length of a word for it to be considered as a possible player name when using <see cref="autocomplete"/>.
        /// </summary>
        public int minimumCompleteLength = 3;

        /// <summary>
        /// The threshold above (or equal to) which the word must match a player name to be corrected, where 1.0 is 100%.
        /// </summary>
        public double autocompleteThreshold = 0.7;

        /// <summary>
        /// Retrieves the chat message type based on whether <see cref="announceAsMessage"/> is true.
        /// </summary>
        /// <returns>The message type.</returns>
        internal EnumChatType GetChatEnumType()
        {
            return announceAsMessage == true ? EnumChatType.OthersMessage : EnumChatType.Notification;
        }
    }
}