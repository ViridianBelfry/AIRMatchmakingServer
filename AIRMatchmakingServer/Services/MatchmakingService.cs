using System.Collections.Concurrent;
using AIRMatchmakingServer.Utils;
using System.Linq;
using AIRMatchmakingServer.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using AIR.Shared.Contracts.Matchmaking;

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

        public PlayerJoinResponse EnqueuePlayer(PlayerJoinRequest player, out List<QueueEntry>? readyEntries)
        {
            readyEntries = null;

            var entry = new QueueEntry
            {
                PlayerId = player.PlayerId,
                MMR = player.MMR,
                QueueType = player.QueueType,
                LobbySize = player.LobbySize,
                BotFill = player.BotFill,
                BotCount = player.BotCount
            };

            entry.LastSeenAtUtc = DateTime.UtcNow;

            _tickets[entry.TicketId] = entry;

            var queue = _queues[entry.LobbySize];
            queue.Enqueue(entry);

            var response = BuildJoinResponse(entry);

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
                    readyEntries = candidates;
                    foreach (var c in candidates)
                    {
                        _tickets.TryRemove(c.TicketId, out _);
                    }
                    return response;
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

            return response;
        }

        private PlayerJoinResponse BuildJoinResponse(QueueEntry entry)
        {
            return new PlayerJoinResponse
            {
                TicketId = entry.TicketId,
                QueueState = BuildQueueStateSnapshot(entry),
                Lease = IssueLease()
            };
        }

        private QueueLease IssueLease()
        {
            var issued = DateTime.UtcNow;
            var expires = issued + _heartbeatTtl;
            var heartbeatIntervalSeconds = Math.Max(1, (int)Math.Round(_heartbeatTtl.TotalSeconds * 0.5));
            var graceSeconds = Math.Max(1, (int)Math.Round(_heartbeatTtl.TotalSeconds * 0.25));
            return new QueueLease(issued, expires, heartbeatIntervalSeconds, graceSeconds);
        }

        private QueueStateSnapshot BuildQueueStateSnapshot(QueueEntry entry)
        {
            var queue = _queues[entry.LobbySize];
            var snapshotEntries = queue.ToArray();
            var matchSize = Math.Max(1, LobbySizeUtils.ToPlayerCount(entry.LobbySize));
            var expectedGroups = Math.Max(0, (int)Math.Ceiling(snapshotEntries.Length / (double)matchSize));
            var expectedWaitSeconds = expectedGroups == 0
                ? 0
                : (int)Math.Ceiling(expectedGroups * _heartbeatTtl.TotalSeconds);

            var mmrValues = snapshotEntries
                .Where(e => !string.Equals(e.TicketId, entry.TicketId, StringComparison.Ordinal))
                .Select(e => e.MMR)
                .Where(m => m > 0)
                .ToList();

            var averageMmr = mmrValues.Count > 0
                ? (int)Math.Round(mmrValues.Average())
                : entry.MMR;

            return new QueueStateSnapshot
            {
                QueueType = entry.QueueType,
                LobbyTargetSize = entry.LobbySize,
                ExpectedWaitSeconds = expectedWaitSeconds,
                ActiveTickets = snapshotEntries.Length,
                AverageOpponentMmr = averageMmr
            };
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

        public HeartbeatAck? Heartbeat(HeartbeatPing? ping)
        {
            if (ping == null || string.IsNullOrWhiteSpace(ping.TicketId))
            {
                _logger?.LogWarning("Heartbeat rejected: missing payload or ticket id.");
                return null;
            }

            if (_tickets.TryGetValue(ping.TicketId, out var entry) && entry.Status == QueueStatus.Queued)
            {
                entry.LastSeenAtUtc = DateTime.UtcNow;
                var ack = new HeartbeatAck
                {
                    TicketId = entry.TicketId,
                    ServerTimestampUtc = DateTime.UtcNow,
                    Lease = IssueLease()
                };

                _logger?.LogInformation("Heartbeat received: TicketId={TicketId}", ping.TicketId);
                return ack;
            }

            _logger?.LogWarning("Heartbeat rejected: TicketId={TicketId} not active.", ping.TicketId);
            return null;
        }

        public LeaveQueueResult Leave(LeaveQueueRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TicketId))
            {
                return new LeaveQueueResult
                {
                    Success = false,
                    FailureReason = LeaveQueueFailureReason.TicketNotFound,
                    Message = "ticketId is required."
                };
            }

            if (!_tickets.TryGetValue(request.TicketId, out var entry))
            {
                return new LeaveQueueResult
                {
                    Success = false,
                    FailureReason = LeaveQueueFailureReason.TicketNotFound,
                    Message = "Ticket not found."
                };
            }

            if (!string.IsNullOrWhiteSpace(request.PlayerId) &&
                !string.Equals(request.PlayerId, entry.PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return new LeaveQueueResult
                {
                    Success = false,
                    FailureReason = LeaveQueueFailureReason.Unknown,
                    Message = "PlayerId does not match the ticket owner."
                };
            }

            switch (entry.Status)
            {
                case QueueStatus.Queued:
                    entry.Status = QueueStatus.Cancelled;
                    return new LeaveQueueResult
                    {
                        Success = true,
                        FailureReason = LeaveQueueFailureReason.None,
                        Message = "Removed from queue."
                    };

                case QueueStatus.Cancelled:
                    return new LeaveQueueResult
                    {
                        Success = false,
                        FailureReason = LeaveQueueFailureReason.Unknown,
                        Message = "Ticket already cancelled."
                    };

                case QueueStatus.Expired:
                    return new LeaveQueueResult
                    {
                        Success = false,
                        FailureReason = LeaveQueueFailureReason.LeaseExpired,
                        Message = "Ticket expired before leave request was processed."
                    };

                default:
                    return new LeaveQueueResult
                    {
                        Success = false,
                        FailureReason = LeaveQueueFailureReason.Unknown,
                        Message = $"Ticket is in unexpected state: {entry.Status}."
                    };
            }
        }

        public Dictionary<LobbySize, List<QueueEntry>> GetQueueSnapshot()
        {
            // Safe snapshot of current queues for diagnostics
            return _queues.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToArray().ToList()
            );
        }

        public int GetHeartbeatTtlSeconds() => (int)_heartbeatTtl.TotalSeconds;

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
