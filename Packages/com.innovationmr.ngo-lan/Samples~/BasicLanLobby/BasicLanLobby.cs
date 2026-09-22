using System.Collections.Generic;
using InnovationMR.NgoLan.Discovery;
using InnovationMR.NgoLan.Session;
using UnityEngine;

namespace InnovationMR.NgoLan.Samples
{
    /// <summary>
    /// UI-independent wiring example. Connect these methods and SessionsChanged to your world-space MR UI.
    /// </summary>
    public sealed class BasicLanLobby : MonoBehaviour
    {
        [SerializeField] private NgoLanSessionController sessions;
        private IReadOnlyList<LanSessionInfo> discovered = System.Array.Empty<LanSessionInfo>();

        public IReadOnlyList<LanSessionInfo> Discovered => discovered;

        private void OnEnable()
        {
            sessions.Discovery.SessionsChanged += OnSessionsChanged;
            sessions.StartBrowsing();
        }

        private void OnDisable()
        {
            if (sessions != null && sessions.Discovery != null)
                sessions.Discovery.SessionsChanged -= OnSessionsChanged;
        }

        public void Host() => sessions.StartHost(advertise: true);
        public void Join(int index)
        {
            if (index >= 0 && index < discovered.Count)
                sessions.Join(discovered[index]);
        }
        public void Leave() => sessions.Shutdown();

        private void OnSessionsChanged(IReadOnlyList<LanSessionInfo> value) => discovered = value;
    }
}
