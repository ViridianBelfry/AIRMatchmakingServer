using System.Collections.Concurrent;
using AIRMatchmakingServer.Models;

namespace AIRMatchmakingServer.Services
{
    public class MatchmakingService
    {
        private readonly ConcurrentQueue<PlayerJoinRequest> _queue = new();
        private const int MaxPlayersPerMatch = 32;

        public bool TryAddPlayer(PlayerJoinRequest player, out List<PlayerJoinRequest>? match)
        {
            _queue.Enqueue(player);

            if (_queue.Count >= MaxPlayersPerMatch)
            {
                match = new List<PlayerJoinRequest>();

                for (int i = 0; i < MaxPlayersPerMatch; i++)
                {
                    if (_queue.TryDequeue(out var p))
                        match.Add(p);
                }

                return true; // match ready
            }

            match = null;
            return false; // keep waiting
        }
    }
}
