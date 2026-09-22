using System;

namespace InnovationMR.NgoLan.Discovery
{
    [Serializable]
    public readonly struct LanSessionInfo : IEquatable<LanSessionInfo>
    {
        public string Id { get; }
        public string Name { get; }
        public string GameVersion { get; }
        public string Metadata { get; }
        public string Address { get; }
        public ushort Port { get; }
        public int PlayerCount { get; }
        public int MaxPlayers { get; }

        public LanSessionInfo(string id, string name, string gameVersion, string metadata,
            string address, ushort port, int playerCount, int maxPlayers)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            GameVersion = gameVersion ?? string.Empty;
            Metadata = metadata ?? string.Empty;
            Address = address ?? string.Empty;
            Port = port;
            PlayerCount = playerCount;
            MaxPlayers = maxPlayers;
        }

        public bool Equals(LanSessionInfo other) =>
            Id == other.Id && Name == other.Name && GameVersion == other.GameVersion &&
            Metadata == other.Metadata && Address == other.Address && Port == other.Port &&
            PlayerCount == other.PlayerCount && MaxPlayers == other.MaxPlayers;

        public override bool Equals(object obj) => obj is LanSessionInfo other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Name, GameVersion, Metadata,
            Address, Port, PlayerCount, MaxPlayers);
    }
}
