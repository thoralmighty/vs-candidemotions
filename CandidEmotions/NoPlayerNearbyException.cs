using System;
using System.Runtime.Serialization;

namespace CandidEmotions
{
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
