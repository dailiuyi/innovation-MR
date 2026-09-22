using System;
using InnovationMR.NgoLan.Discovery;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace InnovationMR.NgoLan.Session
{
    [DisallowMultipleComponent]
    public sealed class NgoLanSessionController : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private LanDiscoveryService discovery;
        [SerializeField, Range(1, 65535)] private int gamePort = 7777;
        [SerializeField, Min(1)] private int maxPlayers = 8;
        [SerializeField] private string gameVersion = "0.1.0";
        [SerializeField] private string sessionName = "MR Session";
        [SerializeField, TextArea] private string metadata;
        [SerializeField] private bool useConnectionApproval = true;

        private string sessionId;
        private bool subscribed;

        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action SessionStopped;
        public event Action<string> SessionError;

        /// <summary>
        /// Optional server-side policy. Return false and provide a rejection reason to deny a client.
        /// The package applies its player-cap check before this callback.
        /// </summary>
        public Func<NetworkManager.ConnectionApprovalRequest, (bool Approved, string Reason)> ConnectionApprover { get; set; }

        public NetworkManager NetworkManager => networkManager;
        public LanDiscoveryService Discovery => discovery;
        public bool IsRunning => networkManager != null && networkManager.IsListening;
        public bool IsConnectedClient => networkManager != null && networkManager.IsConnectedClient;
        public string LastDisconnectReason => networkManager != null ? networkManager.DisconnectReason : string.Empty;

        private void Reset()
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            discovery = FindFirstObjectByType<LanDiscoveryService>();
        }

        private void Awake()
        {
            sessionId = Guid.NewGuid().ToString("N");
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        public bool StartHost(bool advertise = true)
        {
            if (!Prepare((ushort)gamePort, "0.0.0.0")) return false;
            discovery?.StopBrowsing();
            bool started = networkManager.StartHost();
            if (started && advertise)
                discovery?.StartAdvertising(BuildBeacon);
            else if (!started)
                Fail("NGO StartHost returned false.");
            return started;
        }

        public bool StartServer(bool advertise = true)
        {
            if (!Prepare((ushort)gamePort, "0.0.0.0")) return false;
            discovery?.StopBrowsing();
            bool started = networkManager.StartServer();
            if (started && advertise)
                discovery?.StartAdvertising(BuildBeacon);
            else if (!started)
                Fail("NGO StartServer returned false.");
            return started;
        }

        public bool Join(LanSessionInfo session, byte[] connectionPayload = null)
        {
            if (session.Port == 0 || string.IsNullOrWhiteSpace(session.Address))
            {
                Fail("Discovered session has no valid endpoint.");
                return false;
            }
            if (!string.IsNullOrWhiteSpace(gameVersion) && session.GameVersion != gameVersion)
            {
                Fail($"Game version mismatch. Local={gameVersion}, Remote={session.GameVersion}");
                return false;
            }
            if (!Prepare(session.Port, null, session.Address)) return false;
            discovery?.StopBrowsing();
            networkManager.NetworkConfig.ConnectionData = connectionPayload ?? Array.Empty<byte>();
            bool started = networkManager.StartClient();
            if (!started) Fail("NGO StartClient returned false.");
            return started;
        }

        public bool JoinDirect(string address, ushort port, byte[] connectionPayload = null) =>
            Join(new LanSessionInfo(string.Empty, "Direct", gameVersion, string.Empty,
                address, port, 0, Math.Max(1, maxPlayers)), connectionPayload);

        public void StartBrowsing()
        {
            if (!IsRunning) discovery?.StartBrowsing();
        }

        public void Shutdown()
        {
            discovery?.StopAdvertising();
            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();
            else
                StartBrowsing();
        }

        private bool Prepare(ushort port, string listenAddress, string connectAddress = null)
        {
            if (networkManager == null)
            {
                Fail("NetworkManager reference is missing.");
                return false;
            }
            if (networkManager.NetworkConfig.NetworkTransport is not UnityTransport transport)
            {
                Fail("NetworkManager must use UnityTransport.");
                return false;
            }

            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.ConnectionApproval = useConnectionApproval;
            networkManager.ConnectionApprovalCallback = useConnectionApproval ? ApproveConnection : null;
            string address = string.IsNullOrWhiteSpace(connectAddress)
                ? transport.ConnectionData.Address
                : connectAddress;
            string listen = listenAddress ?? transport.ConnectionData.ServerListenAddress;
            transport.SetConnectionData(address, port, listen);
            Subscribe();
            return true;
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = networkManager.NetworkConfig.PlayerPrefab != null;
            response.Pending = false;

            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }

            if (networkManager.ConnectedClientsIds.Count >= maxPlayers)
            {
                response.Approved = false;
                response.Reason = "Session is full.";
                return;
            }

            if (ConnectionApprover != null)
            {
                (bool approved, string reason) = ConnectionApprover(request);
                response.Approved = approved;
                response.Reason = approved ? string.Empty : reason ?? "Connection rejected.";
                return;
            }

            response.Approved = true;
        }

        private LanSessionInfo BuildBeacon() => new(
            sessionId, sessionName, gameVersion, metadata, string.Empty, (ushort)gamePort,
            networkManager != null ? networkManager.ConnectedClientsIds.Count : 0, maxPlayers);

        private void Subscribe()
        {
            if (subscribed || networkManager == null) return;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnServerStopped += OnStopped;
            networkManager.OnClientStopped += OnStopped;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || networkManager == null) return;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStopped -= OnStopped;
            networkManager.OnClientStopped -= OnStopped;
            subscribed = false;
        }

        private void OnClientConnected(ulong id) => PeerConnected?.Invoke(id);
        private void OnClientDisconnected(ulong id) => PeerDisconnected?.Invoke(id);
        private void OnStopped(bool _)
        {
            discovery?.StopAdvertising();
            StartBrowsing();
            SessionStopped?.Invoke();
        }

        private void Fail(string message)
        {
            Debug.LogWarning($"[{nameof(NgoLanSessionController)}] {message}", this);
            SessionError?.Invoke(message);
        }

        private void OnDestroy()
        {
            discovery?.StopAdvertising();
            Unsubscribe();
        }
    }
}
