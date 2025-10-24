using System.Collections.Concurrent;
using AIRMatchmakingServer.Models;
using AIRMatchmakingServer.Utils;
using System.Linq;
using AIRMatchmakingServer.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AIRMatchmakingServer.Services
{
    public class MatchmakingService
    {
        private readonly Dictionary<LobbySize, ConcurrentQueue<QueueEntry>> _queues = new()
        {
            [LobbySize.Small] = new(),
            [LobbySize.Medium] = new(),
            [LobbySize.Large] = new()
        };

        private readonly ConcurrentDictionary<string, QueueEntry> _tickets = new();
        private readonly TimeSpan _heartbeatTtl;

        private readonly ILogger<MatchmakingService>? _logger;

        public MatchmakingService(IOptions<MatchmakingOptions> options, ILogger<MatchmakingService>? logger = null)
        {
            var ttlSeconds = Math.Max(5, options?.Value?.HeartbeatTtlSeconds ?? 30); // guardrail min 5s
            _heartbeatTtl = TimeSpan.FromSeconds(ttlSeconds);
            _logger = logger;
        }

        public bool TryAddPlayer(PlayerJoinRequest player, out List<PlayerJoinRequest>? match)
            => TryAddPlayer(player, out match, out _);

        public bool TryAddPlayer(PlayerJoinRequest player, out List<PlayerJoinRequest>? match, out string ticketId)
        {
            match = null;

            var entry = new QueueEntry
            {
                PlayerId = player.PlayerId,
                MMR = player.MMR,
                QueueType = player.QueueType,
                LobbySize = player.LobbySize,
                BotFill = player.BotFill,
                BotCount = player.BotCount
            };

            _tickets[entry.TicketId] = entry;

            var queue = _queues[entry.LobbySize];
            queue.Enqueue(entry);

            ticketId = entry.TicketId;

            int matchSize = LobbySizeUtils.ToPlayerCount(entry.LobbySize);

            if (queue.Count >= matchSize)
            {
                var candidates = new List<QueueEntry>();
                while (candidates.Count < matchSize && queue.TryDequeue(out var e))
                {
                    if (IsEntryActive(e))
                    {
                        candidates.Add(e);
                    }
                    else
                    {
                        // drop cancelled/expired entries
                        _tickets.TryRemove(e.TicketId, out _);
                    }
                }

                if (candidates.Count == matchSize)
                {
                    match = candidates.Select(c => c.ToRequest()).ToList();
                    foreach (var c in candidates)
                    {
                        _tickets.TryRemove(c.TicketId, out _);
                    }
                    return true; // match ready
                }
                else
                {
                    // Not enough valid players; re-enqueue valid candidates to preserve them
                    foreach (var c in candidates)
                    {
                        queue.Enqueue(c);
                    }
                }
            }

            match = null;
            return false; // keep waiting
        }

        private bool IsEntryActive(QueueEntry e)
        {
            if (e.Status != QueueStatus.Queued) return false;
            if (DateTime.UtcNow - e.LastSeenAtUtc > _heartbeatTtl)
            {
                e.Status = QueueStatus.Expired;
                return false;
            }
            return true;
        }

        public bool Heartbeat(string ticketId)
        {
            if (_tickets.TryGetValue(ticketId, out var entry))
            {
                if (entry.Status == QueueStatus.Queued)
                {
                    entry.LastSeenAtUtc = DateTime.UtcNow;
                    return true;
                }
            }
            return false;
        }

        public bool Leave(string ticketId)
        {
            if (_tickets.TryGetValue(ticketId, out var entry))
            {
                entry.Status = QueueStatus.Cancelled;
                // Leave it in queue; it will be skipped when dequeued.
                return true;
            }
            return false;
        }

        public Dictionary<LobbySize, List<QueueEntry>> GetQueueSnapshot()
        {
            // Safe snapshot of current queues for diagnostics
            return _queues.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToArray().ToList()
            );
        }

        public int GetTtlSeconds() => (int)_heartbeatTtl.TotalSeconds;

        // Background sweep to mark expired tickets and compact queues
        public (int markedExpired, int removedTickets, int keptEntries) SweepOnce()
        {
            int markedExpired = 0;
            int removedTickets = 0;
            int keptEntries = 0;

            // Mark stale tickets as expired
            foreach (var kvp in _tickets)
            {
                var entry = kvp.Value;
                if (entry.Status == QueueStatus.Queued && DateTime.UtcNow - entry.LastSeenAtUtc > _heartbeatTtl)
                {
                    entry.Status = QueueStatus.Expired;
                    markedExpired++;
                }
            }

            // Compact each queue by removing cancelled/expired entries
            foreach (var q in _queues.Values)
            {
                var keep = new List<QueueEntry>();
                while (q.TryDequeue(out var e))
                {
                    if (IsEntryActive(e))
                    {
                        keep.Add(e);
                    }
                    else
                    {
                        // remove ticket tracking for inactive entries
                        if (_tickets.TryRemove(e.TicketId, out _))
                            removedTickets++;
                    }
                }

                // preserve order by re-enqueuing in the same sequence
                foreach (var e in keep)
                {
                    q.Enqueue(e);
                }
                keptEntries += keep.Count;
            }

            _logger?.LogDebug("Sweep completed: MarkedExpired={MarkedExpired}, RemovedTickets={RemovedTickets}, KeptEntries={KeptEntries}",
                markedExpired, removedTickets, keptEntries);

            return (markedExpired, removedTickets, keptEntries);
        }
    }
}
