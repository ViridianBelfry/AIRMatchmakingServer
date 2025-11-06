
using AIRMatchmakingServer.Services;
using AIRMatchmakingServer.Utils;
using Microsoft.AspNetCore.SignalR;

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

        public async Task JoinQueue(string mode, int mmr)
        {
            Console.WriteLine($"JoinQueue start: {mode} {mmr}");
            await Task.Delay(1000);
            // return Task.CompletedTask;
        }

        /*
        public async Task JoinQueue(PlayerJoinRequest request)
        {
            _logger.LogWarning($"JoinQueue has been entered with request: {request}");
            var ip = Context?.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation("Hub JoinQueue: PlayerId={PlayerId}, QueueType={QueueType}, LobbySize={LobbySize}, IP={IP}",
                request?.PlayerId, request?.QueueType, request?.LobbySize, ip);

            var validationError = RequestValidator.ValidateJoinRequest(request);
            if (validationError != null)
            {
                throw new HubException("Invalid join request");
            }

            // Immediate match with bots if requested
            if (request.BotFill || (request.BotCount.HasValue && request.BotCount.Value > 0))
            {
                var matchSize = LobbySizeUtils.ToPlayerCount(request.LobbySize);
                var botsToAdd = request.BotFill
                    ? Math.Max(0, matchSize - 1)
                    : Math.Min(Math.Max(0, request.BotCount ?? 0), Math.Max(0, matchSize - 1));

                var players = new List<string> { request.PlayerId };
                for (int i = 0; i < botsToAdd; i++)
                {
                    players.Add($"BOT_{Guid.NewGuid().ToString().Substring(0, 5)}");
                }

                var gameUrlImmediate = $"https://game-instance-{Guid.NewGuid().ToString().Substring(0, 8)}.alpha.com";

                var targets = _connections.GetConnections(request.PlayerId);
                if (targets.Count > 0)
                {
                    await Clients.Clients(targets).SendAsync("MatchFound", new
                    {
                        message = "Match created with bots.",
                        gameUrl = gameUrlImmediate,
                        players,
                        botCount = botsToAdd,
                        ticketId = (string?)null
                    });
                }

                _logger.LogInformation("Hub immediate bot match sent: LobbySize={LobbySize}, Human={PlayerId}, Bots={BotCount}", request.LobbySize, request.PlayerId, botsToAdd);
                return;
            }

            if (_matchmakingService.TryAddPlayer(request, out var match, out var ticketId))
            {
                var gameUrl = $"https://game-instance-{Guid.NewGuid().ToString().Substring(0, 8)}.alpha.com";

                if (match == null)
                {
                    _logger.LogWarning("Match could not be created after TryAddPlayer signaled ready.");
                    return;
                }

                var players = match.Select(p => p.PlayerId).ToList();
                foreach (var pid in players)
                {
                    var targets = _connections.GetConnections(pid);
                    if (targets.Count > 0)
                    {
                        await Clients.Clients(targets).SendAsync("MatchFound", new
                        {
                            message = "Match found!",
                            gameUrl,
                            players,
                            ticketId
                        });
                    }
                    else
                    {
                        _logger.LogWarning("No active connections to deliver MatchFound for PlayerId={PlayerId}", pid);
                    }
                }

                _logger.LogInformation("Hub MatchFound sent: LobbySize={LobbySize}, Players={Players}", request.LobbySize, string.Join(",", players));
            }
            else
            {
                var targets = _connections.GetConnections(request.PlayerId);
                if (targets.Count > 0)
                {
                    await Clients.Clients(targets).SendAsync("Queued", new { message = "Waiting for match...", ticketId, ttlSeconds = _matchmakingService.GetTtlSeconds() });
                }

                _logger.LogInformation("Hub player queued and waiting: PlayerId={PlayerId}, LobbySize={LobbySize}", request.PlayerId, request.LobbySize);
            }
        }
        */

        public Task<bool> Heartbeat(string ticketId)
        {
            var ok = _matchmakingService.Heartbeat(ticketId);
            _logger.LogDebug("Hub Heartbeat: TicketId={TicketId}, Ok={Ok}", ticketId, ok);
            return Task.FromResult(ok);
        }

        public Task<bool> Leave(string ticketId)
        {
            var ok = _matchmakingService.Leave(ticketId);
            _logger.LogInformation("Hub Leave: TicketId={TicketId}, Ok={Ok}", ticketId, ok);
            return Task.FromResult(ok);
        }
    }
}
