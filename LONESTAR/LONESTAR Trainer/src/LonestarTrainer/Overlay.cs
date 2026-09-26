using UnityEngine;

namespace LonestarTrainer
{
    /// <summary>Transient one-line confirmations, shown just above the status strip.</summary>
    internal static class Overlay
    {
        private const float HoldSeconds = 2.5f;

        private static string _message;
        private static float _shownAt;

        public static void Flash(string message)
        {
            _message = message;
            _shownAt = Time.realtimeSinceStartup;
            Debug.Log("[LonestarTrainer] " + message);
        }

        public static string Current
        {
            get
            {
                if (_message == null) return null;
                if (Time.realtimeSinceStartup - _shownAt > HoldSeconds)
                {
                    _message = null;
                    return null;
                }
                return _message;
            }
        }
    }
}
