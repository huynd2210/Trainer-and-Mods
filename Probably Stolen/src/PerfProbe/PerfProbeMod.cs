using UnityEngine;

namespace PerfProbe
{
    // Perf Probe: a lag diagnostic. Times the other mods' code and logs slow frames.
    public class PerfProbeMod : IMod
    {
        private GameObject host;

        public void Init(ModManifest manifest) { }

        public void OnEnable()
        {
            if (host != null)
                return;
            host = new GameObject("PerfProbe");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        public void OnDisable()
        {
            // The timing patches stay until the game restarts; only the frame log stops.
            if (host != null)
                Object.Destroy(host);
            host = null;
        }
    }
}
