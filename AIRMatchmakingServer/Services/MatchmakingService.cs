using System.Collections.Concurrent;
using AIRMatchmakingServer.Models;
using AIRMatchmakingServer.Utils;
using System.Linq;

namespace AIRMatchmakingServer.Services
{
    public class MatchmakingService
    {
        private readonly Dictionary<LobbySize, ConcurrentQueue<PlayerJoinRequest>> _queues = new()
        {
            [LobbySize.Small] = new(),
            [LobbySize.Medium] = new(),
            [LobbySize.Large] = new()
        };

        //private readonly ConcurrentQueue<PlayerJoinRequest> _queue = new();
        private const int MaxPlayersPerMatch = 32;

        public bool TryAddPlayer(PlayerJoinRequest player, out List<PlayerJoinRequest>? match)
        {
            match = null;
            var queue = _queues[player.LobbySize];
            queue.Enqueue(player);

            int matchSize = LobbySizeUtils.ToPlayerCount(player.LobbySize);

            if (queue.Count >= matchSize)
            {
                match = new List<PlayerJoinRequest>();

                for (int i = 0; i < MaxPlayersPerMatch; i++)
                {
                    if (queue.TryDequeue(out var p))
                        match.Add(p);
                }

                return true; // match ready
            }

            match = null;
            return false; // keep waiting
        }

        public Dictionary<LobbySize, List<PlayerJoinRequest>> GetQueueSnapshot()
        {
            // Safe snapshot of current queues for diagnostics
            return _queues.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToArray().ToList()
            );
        }
    }
}
