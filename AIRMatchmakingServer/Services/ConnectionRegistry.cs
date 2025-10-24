using System.Collections.Concurrent;

namespace AIRMatchmakingServer.Services
{
    public class ConnectionRegistry
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _byPlayer = new();
        private readonly ConcurrentDictionary<string, string> _byConnection = new();

        public void BindConnection(string playerId, string connectionId)
        {
            _byConnection[connectionId] = playerId;
            var set = _byPlayer.GetOrAdd(playerId, _ => new HashSet<string>());
            lock (set)
            {
                set.Add(connectionId);
            }
        }

        public string? UnbindConnection(string connectionId)
        {
            if (_byConnection.TryRemove(connectionId, out var playerId))
            {
                if (_byPlayer.TryGetValue(playerId, out var set))
                {
                    lock (set)
                    {
                        set.Remove(connectionId);
                        if (set.Count == 0)
                        {
                            _byPlayer.TryRemove(playerId, out _);
                        }
                    }
                }
                return playerId;
            }
            return null;
        }

        public IReadOnlyList<string> GetConnections(string playerId)
        {
            if (_byPlayer.TryGetValue(playerId, out var set))
            {
                lock (set)
                {
                    return set.ToList();
                }
            }

            return Array.Empty<string>();
        }
    }
}

