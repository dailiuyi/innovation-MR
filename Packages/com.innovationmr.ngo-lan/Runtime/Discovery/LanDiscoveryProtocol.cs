using System;
using System.Net;
using System.Text;
using UnityEngine;

namespace InnovationMR.NgoLan.Discovery
{
    internal static class LanDiscoveryProtocol
    {
        internal const int Magic = 0x494D5231; // "IMR1"
        internal const int Format = 1;
        internal const int MaxPacketBytes = 4096;
        private const int MaxIdLength = 128;
        private const int MaxNameLength = 64;
        private const int MaxVersionLength = 32;
        private const int MaxMetadataLength = 1024;

        [Serializable]
        private struct BeaconPayload
        {
            public int magic;
            public int format;
            public string id;
            public string name;
            public string version;
            public string metadata;
            public int players;
            public int maxPlayers;
            public int port;
        }

        internal static byte[] Encode(LanSessionInfo session)
        {
            var payload = new BeaconPayload
            {
                magic = Magic,
                format = Format,
                id = Limit(session.Id, MaxIdLength),
                name = Limit(session.Name, MaxNameLength),
                version = Limit(session.GameVersion, MaxVersionLength),
                metadata = Limit(session.Metadata, MaxMetadataLength),
                players = session.PlayerCount,
                maxPlayers = session.MaxPlayers,
                port = session.Port
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        }

        internal static bool TryDecode(byte[] data, IPEndPoint sender, out LanSessionInfo session)
        {
            session = default;
            if (data == null || data.Length == 0 || data.Length > MaxPacketBytes || sender == null)
                return false;

            try
            {
                var payload = JsonUtility.FromJson<BeaconPayload>(Encoding.UTF8.GetString(data));
                if (payload.magic != Magic || payload.format != Format ||
                    payload.port < 1 || payload.port > 65535 ||
                    payload.players < 0 || payload.maxPlayers < 1 || payload.players > payload.maxPlayers ||
                    !Valid(payload.id, MaxIdLength) || !Valid(payload.name, MaxNameLength) ||
                    !Valid(payload.version, MaxVersionLength) || !Valid(payload.metadata, MaxMetadataLength))
                    return false;

                session = new LanSessionInfo(payload.id, payload.name, payload.version, payload.metadata,
                    sender.Address.ToString(), (ushort)payload.port, payload.players, payload.maxPlayers);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Valid(string value, int maxLength) => value != null && value.Length <= maxLength;
        private static string Limit(string value, int maxLength)
        {
            value ??= string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
