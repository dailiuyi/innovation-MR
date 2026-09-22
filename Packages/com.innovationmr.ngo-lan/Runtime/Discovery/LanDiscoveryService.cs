using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace InnovationMR.NgoLan.Discovery
{
    public sealed class LanDiscoveryService : MonoBehaviour
    {
        [SerializeField, Min(1)] private int discoveryPort = 47777;
        [SerializeField, Min(0.1f)] private float broadcastIntervalSeconds = 1f;
        [SerializeField, Min(0.5f)] private float sessionTimeoutSeconds = 4f;
        [SerializeField, Range(1, 256)] private int maxSessions = 64;
        [SerializeField, Range(8, 2048)] private int maxQueuedPackets = 256;
        [SerializeField, Range(1, 256)] private int maxPacketsPerFrame = 64;

        private readonly Queue<LanSessionInfo> received = new();
        private readonly object receivedGate = new();
        private LanSessionRegistry registry;
        private UdpClient browser;
        private UdpClient advertiser;
        private CancellationTokenSource browseCancellation;
        private Coroutine advertiseCoroutine;
        private volatile bool browseLoopAlive;
        private string pendingBackgroundError;
        private Func<LanSessionInfo> beaconFactory;

        public event Action<IReadOnlyList<LanSessionInfo>> SessionsChanged;
        public event Action<string> DiscoveryError;
        public IReadOnlyList<LanSessionInfo> Sessions => registry?.Snapshot() ?? Array.Empty<LanSessionInfo>();
        public bool IsBrowsing => browser != null && browseLoopAlive;
        public bool IsAdvertising => advertiseCoroutine != null;

        private void Awake() => registry = new LanSessionRegistry(maxSessions);

        private void Update()
        {
            bool changed = false;
            for (int processed = 0; processed < maxPacketsPerFrame; processed++)
            {
                LanSessionInfo item;
                lock (receivedGate)
                {
                    if (received.Count == 0) break;
                    item = received.Dequeue();
                }
                changed |= registry.Upsert(item, Time.realtimeSinceStartupAsDouble);
            }

            changed |= registry.RemoveExpired(Time.realtimeSinceStartupAsDouble, sessionTimeoutSeconds);
            string backgroundError = Interlocked.Exchange(ref pendingBackgroundError, null);
            if (!string.IsNullOrEmpty(backgroundError)) ReportError(backgroundError);
            if (changed) Publish();
        }

        public bool StartBrowsing()
        {
            if (IsBrowsing) return true;
            StopBrowsing();
            try
            {
                browser = new UdpClient(AddressFamily.InterNetwork);
                browser.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                browser.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
                browseCancellation = new CancellationTokenSource();
                browseLoopAlive = true;
                _ = BrowseLoopAsync(browser, browseCancellation.Token);
                return true;
            }
            catch (Exception exception)
            {
                StopBrowsing();
                ReportError($"LAN browse failed: {exception.Message}");
                return false;
            }
        }

        public void StopBrowsing()
        {
            browseLoopAlive = false;
            browseCancellation?.Cancel();
            browseCancellation?.Dispose();
            browseCancellation = null;
            browser?.Dispose();
            browser = null;
            lock (receivedGate) received.Clear();
            if (registry != null && registry.Clear()) Publish();
        }

        public bool StartAdvertising(Func<LanSessionInfo> createBeacon)
        {
            if (createBeacon == null) throw new ArgumentNullException(nameof(createBeacon));
            if (IsAdvertising) return true;
            try
            {
                advertiser = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                beaconFactory = createBeacon;
                advertiseCoroutine = StartCoroutine(AdvertiseLoop());
                return true;
            }
            catch (Exception exception)
            {
                StopAdvertising();
                ReportError($"LAN advertise failed: {exception.Message}");
                return false;
            }
        }

        public void StopAdvertising()
        {
            if (advertiseCoroutine != null) StopCoroutine(advertiseCoroutine);
            advertiseCoroutine = null;
            beaconFactory = null;
            advertiser?.Dispose();
            advertiser = null;
        }

        private IEnumerator AdvertiseLoop()
        {
            var target = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
            var wait = new WaitForSecondsRealtime(broadcastIntervalSeconds);
            while (true)
            {
                try
                {
                    byte[] packet = LanDiscoveryProtocol.Encode(beaconFactory());
                    if (packet.Length <= LanDiscoveryProtocol.MaxPacketBytes)
                        advertiser.Send(packet, packet.Length, target);
                }
                catch (Exception exception)
                {
                    ReportError($"LAN advertise send failed: {exception.Message}");
                }
                yield return wait;
            }
        }

        private async Task BrowseLoopAsync(UdpClient socket, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult result = await socket.ReceiveAsync();
                    if (LanDiscoveryProtocol.TryDecode(result.Buffer, result.RemoteEndPoint, out LanSessionInfo session))
                    {
                        lock (receivedGate)
                        {
                            if (received.Count >= maxQueuedPackets) received.Dequeue();
                            received.Enqueue(session);
                        }
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (SocketException exception) when (token.IsCancellationRequested || exception.SocketErrorCode == SocketError.Interrupted) { }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref pendingBackgroundError, $"LAN browse stopped: {exception.Message}");
            }
            finally
            {
                if (ReferenceEquals(browser, socket)) browseLoopAlive = false;
            }
        }

        private void Publish() => SessionsChanged?.Invoke(registry.Snapshot());
        private void ReportError(string message)
        {
            Debug.LogWarning($"[{nameof(LanDiscoveryService)}] {message}", this);
            DiscoveryError?.Invoke(message);
        }

        private void OnDisable()
        {
            StopAdvertising();
            StopBrowsing();
        }
    }
}
