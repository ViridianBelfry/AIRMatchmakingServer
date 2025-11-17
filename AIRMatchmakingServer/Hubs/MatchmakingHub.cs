
using AIRMatchmakingServer.Services;
using AIRMatchmakingServer.Utils;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Linq;

namespace AIRMatchmakingServer.Hubs
{
    public class MatchmakingHub : Hub
    {
        private readonly ILogger<MatchmakingHub> _logger;
        private readonly MatchmakingService _matchmakingService;
        private readonly ConnectionRegistry _connections;

        public MatchmakingHub(
            ILogger<MatchmakingHub> logger,
            MatchmakingService matchmakingService,
            ConnectionRegistry connections)
        {
            _logger = logger;
            _matchmakingService = matchmakingService;
            _connections = connections;
        }

        public override Task OnConnectedAsync()
        {
            _logger.LogInformation("Hub connected: ConnId={ConnId}", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var playerId = _connections.UnbindConnection(Context.ConnectionId);
            _logger.LogInformation("Hub disconnected: ConnId={ConnId}, PlayerId={PlayerId}", Context.ConnectionId, playerId ?? "unknown");
            return base.OnDisconnectedAsync(exception);
        }

        public Task Identify(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new HubException("PlayerId is required");

            _connections.BindConnection(playerId, Context.ConnectionId);
            _logger.LogInformation("Identify: PlayerId={PlayerId} ConnId={ConnId}", playerId, Context.ConnectionId);
            return Task.CompletedTask;
        }

        public async Task<PlayerJoinResponse> JoinQueue(PlayerJoinRequest? request)
        {
            if (request == null)
            {
                _logger.LogWarning("JoinQueue invoked with null request payload.");
                throw new HubException("Request payload is required.");
            }

            var joinRequest = request!;
            var ip = Context?.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation("Hub JoinQueue: PlayerId={PlayerId}, QueueType={QueueType}, LobbySize={LobbySize}, IP={IP}",
                joinRequest.PlayerId, joinRequest.QueueType, joinRequest.LobbySize, ip);

            var validationError = RequestValidator.ValidateJoinRequest(joinRequest);
            if (validationError != null)
            {
                _logger.LogWarning("JoinQueue validation failed for PlayerId={PlayerId}: {Error}", joinRequest.PlayerId, validationError);
                throw new HubException(validationError);
            }

            // Immediate match with bots if requested
            if (joinRequest.BotFill || (joinRequest.BotCount.HasValue && joinRequest.BotCount.Value > 0))
            {
                var matchSize = LobbySizeUtils.ToPlayerCount(joinRequest.LobbySize);
                var botsToAdd = joinRequest.BotFill
                    ? Math.Max(0, matchSize - 1)
                    : Math.Min(Math.Max(0, joinRequest.BotCount ?? 0), Math.Max(0, matchSize - 1));

                var players = new List<string> { joinRequest.PlayerId };
                for (int i = 0; i < botsToAdd; i++)
                {
                    players.Add($"BOT_{Guid.NewGuid().ToString().Substring(0, 5)}");
                }

                var sessionId = Guid.NewGuid().ToString("N");
                var host = $"game-instance-{sessionId.Substring(0, 8)}.alpha.com";
                var responseTtl = _matchmakingService.GetHeartbeatTtlSeconds();
                var offer = BuildImmediateMatchOffer(joinRequest, players, host, sessionId, responseTtl);
                await SendMatchOffer(joinRequest.PlayerId, offer);

                _logger.LogInformation("Hub immediate bot match sent: LobbySize={LobbySize}, Human={PlayerId}, Bots={BotCount}", joinRequest.LobbySize, joinRequest.PlayerId, botsToAdd);
                return BuildImmediateJoinResponse(joinRequest);
            }

            var joinResponse = _matchmakingService.EnqueuePlayer(joinRequest, out var readyEntries);
            await BroadcastQueued(joinRequest.PlayerId, joinResponse);

            if (readyEntries != null && readyEntries.Count > 0)
            {
                var sessionId = Guid.NewGuid().ToString("N");
                var host = $"game-instance-{sessionId.Substring(0, 8)}.alpha.com";
                var responseTtl = _matchmakingService.GetHeartbeatTtlSeconds();

                foreach (var entry in readyEntries)
                {
                    var offer = BuildMatchOffer(readyEntries, entry, host, sessionId, responseTtl);
                    await SendMatchOffer(entry.PlayerId, offer);
                }

                _logger.LogInformation("Hub MatchOffer broadcast: LobbySize={LobbySize}, Players={Players}", joinRequest.LobbySize, string.Join(",", readyEntries.Select(e => e.PlayerId)));
            }
            else
            {
                _logger.LogInformation("Hub player queued and waiting: PlayerId={PlayerId}, LobbySize={LobbySize}", joinRequest.PlayerId, joinRequest.LobbySize);
            }

            return joinResponse;
        }

        public Task<HeartbeatAck> Heartbeat(HeartbeatPing? ping)
        {
            var ack = _matchmakingService.Heartbeat(ping);
            if (ack == null)
            {
                _logger.LogWarning("Heartbeat rejected for TicketId={TicketId}", ping?.TicketId ?? "unknown");
                throw new HubException("Ticket not found or expired.");
            }

            _logger.LogDebug("Hub Heartbeat: TicketId={TicketId}, ExpiresAt={ExpiresAtUtc}", ack.TicketId, ack.Lease.ExpiresAtUtc);
            return Task.FromResult(ack);
        }

        public Task<LeaveQueueResult> Leave(LeaveQueueRequest? request)
        {
            var result = _matchmakingService.Leave(request);
            _logger.LogInformation("Hub Leave: TicketId={TicketId}, Success={Success}, Reason={Reason}", request?.TicketId ?? "unknown", result.Success, result.FailureReason);
            return Task.FromResult(result);
        }

        private async Task BroadcastQueued(string playerId, PlayerJoinResponse response)
        {
            var targets = _connections.GetConnections(playerId);
            if (targets.Count > 0)
            {
                var targetList = string.Join(",", targets);
                _logger.LogInformation(
                    "Hub Queued signal sent: PlayerId={PlayerId}, TicketId={TicketId}, ConnectionCount={Connections}, ConnectionIds={ConnectionIds}",
                    playerId,
                    response.TicketId ?? "unknown",
                    targets.Count,
                    targetList);
                await Clients.Clients(targets).SendAsync("Queued", response);
            }
            else
            {
                _logger.LogWarning("Unable to deliver Queued signal: PlayerId={PlayerId} has no active connections.", playerId);
            }
        }

        private async Task SendMatchOffer(string playerId, MatchOffer offer)
        {
            var targets = _connections.GetConnections(playerId);
            if (targets.Count > 0)
            {
                await Clients.Clients(targets).SendAsync("MatchOffer", offer);
            }
            else
            {
                _logger.LogWarning("No active connections to deliver MatchOffer for PlayerId={PlayerId}", playerId);
            }
        }

        private MatchOffer BuildMatchOffer(List<QueueEntry> entries, QueueEntry recipient, string host, string sessionId, int responseTtlSeconds)
        {
            var rosterMembers = entries.Select(e => new MatchRosterMember
            {
                PlayerId = e.PlayerId,
                IsBot = false,
                Mmr = e.MMR > 0 ? e.MMR : null,
                IsLocalPlayer = string.Equals(e.PlayerId, recipient.PlayerId, StringComparison.OrdinalIgnoreCase)
            }).ToList();

            return new MatchOffer
            {
                TicketId = recipient.TicketId,
                SessionId = sessionId,
                Connection = new MatchConnectionInfo
                {
                    Host = host,
                    Port = 0,
                    Transport = "relay",
                    Region = "alpha"
                },
                Roster = new MatchRosterSummary
                {
                    QueueType = recipient.QueueType,
                    LobbySize = recipient.LobbySize,
                    Members = rosterMembers
                },
                OfferExpiresAtUtc = DateTime.UtcNow.AddSeconds(responseTtlSeconds),
                ResponseTtlSeconds = responseTtlSeconds
            };
        }

        private MatchOffer BuildImmediateMatchOffer(PlayerJoinRequest request, List<string> players, string host, string sessionId, int responseTtlSeconds)
        {
            var members = players.Select(pid => new MatchRosterMember
            {
                PlayerId = pid,
                IsBot = !string.Equals(pid, request.PlayerId, StringComparison.OrdinalIgnoreCase),
                Mmr = string.Equals(pid, request.PlayerId, StringComparison.OrdinalIgnoreCase) && request.MMR > 0 ? request.MMR : null,
                IsLocalPlayer = string.Equals(pid, request.PlayerId, StringComparison.OrdinalIgnoreCase)
            }).ToList();

            return new MatchOffer
            {
                TicketId = string.Empty,
                SessionId = sessionId,
                Connection = new MatchConnectionInfo
                {
                    Host = host,
                    Port = 0,
                    Transport = "relay",
                    Region = "alpha"
                },
                Roster = new MatchRosterSummary
                {
                    QueueType = request.QueueType,
                    LobbySize = request.LobbySize,
                    Members = members
                },
                OfferExpiresAtUtc = DateTime.UtcNow.AddSeconds(responseTtlSeconds),
                ResponseTtlSeconds = responseTtlSeconds
            };
        }

        private PlayerJoinResponse BuildImmediateJoinResponse(PlayerJoinRequest request)
        {
            var issued = DateTime.UtcNow;
            return new PlayerJoinResponse
            {
                TicketId = string.Empty,
                QueueState = new QueueStateSnapshot
                {
                    QueueType = request.QueueType,
                    LobbyTargetSize = request.LobbySize,
                    ExpectedWaitSeconds = 0,
                    ActiveTickets = 0,
                    AverageOpponentMmr = request.MMR
                },
                Lease = new QueueLease(issued, issued, 1, 1)
            };
        }
    }
}
