namespace CandidEmotions
{
    /// <summary>
    /// CandidEmotions configuration with defaults.
    /// </summary>
    internal class CandidEmotionsConfig
    {
        public bool enableHugs = true;
        public bool enablePlaceholder = true;
        public bool announceAsMessage = false;
        public string placeholder = "@p";
        public string[] customActions = { };

        public float playerSearchRadius = 30;
        public bool autocomplete = true;
        public bool autocorrect = true;
        public int minimumCompleteLength = 3;
        public double autocompleteThreshold = 0.7;
    }
}