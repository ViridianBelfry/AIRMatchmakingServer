using Microsoft.AspNetCore.Mvc;
using AIRMatchmakingServer.Services;
using AIRMatchmakingServer.Utils;

namespace AIRMatchmakingServer.Controllers
{
    [ApiController]
    [Route("matchmaking")]
    public class MatchmakingController : ControllerBase
    {
        private readonly MatchmakingService _matchmakingService;
        private readonly ILogger<MatchmakingController> _logger;

        public MatchmakingController(MatchmakingService service, ILogger<MatchmakingController> logger)
        {
            _matchmakingService = service;
            _logger = logger;
        }

        [HttpPost("join")]
        public IActionResult JoinQueue([FromBody] string mode, int mmr)
        // public IActionResult JoinQueue([FromBody] PlayerJoinRequest request)
        {
            return Ok();
            /*
            var ip = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation("JoinQueue hit: PlayerId={PlayerId}, QueueType={QueueType}, LobbySize={LobbySize}, IP={IP}",
                request?.PlayerId, request?.QueueType, request?.LobbySize, ip);

            var validationError = RequestValidator.ValidateJoinRequest(request);
            if (validationError != null)
            {
                _logger.LogWarning("JoinQueue validation failed for PlayerId={PlayerId}: {Result}", request?.PlayerId, validationError.GetType().Name);
                return validationError;
            }

            // If the caller requested a bot-filled lobby or explicit bot count, create an immediate match
            if (request.BotFill || (request.BotCount.HasValue && request.BotCount.Value > 0))
            {
                var matchSize = LobbySizeUtils.ToPlayerCount(request.LobbySize);
                var botsToAdd = request.BotFill
                    ? Math.Max(0, matchSize - 1)
                    : Math.Min(Math.Max(0, request.BotCount ?? 0), Math.Max(0, matchSize - 1));

                var matchImmediate = new List<PlayerJoinRequest>
                {
                    new PlayerJoinRequest
                    {
                        PlayerId = request.PlayerId,
                        MMR = request.MMR,
                        LobbySize = request.LobbySize,
                        QueueType = request.QueueType,
                        BotFill = request.BotFill,
                        BotCount = request.BotCount
                    }
                };

                for (int i = 0; i < botsToAdd; i++)
                {
                    matchImmediate.Add(new PlayerJoinRequest
                    {
                        PlayerId = $"BOT_{Guid.NewGuid().ToString().Substring(0, 5)}",
                        MMR = request.MMR,
                        LobbySize = request.LobbySize,
                        QueueType = request.QueueType
                    });
                }

                var gameUrlImmediate = $"https://game-instance-{Guid.NewGuid().ToString().Substring(0, 8)}.alpha.com";
                _logger.LogInformation("Immediate bot match: LobbySize={LobbySize}, Human={PlayerId}, Bots={BotCount}", request.LobbySize, request.PlayerId, botsToAdd);
                return Ok(new
                {
                    message = "Match created with bots.",
                    gameUrl = gameUrlImmediate,
                    players = matchImmediate.Select(p => p.PlayerId).ToList(),
                    botCount = botsToAdd,
                    ticketId = (string?)null,
                    ttlSeconds = _matchmakingService.GetTtlSeconds()
                });
            }

            if (_matchmakingService.TryAddPlayer(request, out var match, out var ticketId))
                {
                    // Simulate spawning a game server and returning its URL
                    var gameUrl = $"https://game-instance-{Guid.NewGuid().ToString().Substring(0, 8)}.alpha.com";

                    if (match == null)
                        return BadRequest("Match could not be created.");

                    // In future: send all players to gameUrl
                    _logger.LogInformation("Match found: LobbySize={LobbySize}, Players={Players}", request.LobbySize, string.Join(",", match.Select(p => p.PlayerId)));
                    return Ok(new
                    {
                        message = "Match found!",
                        gameUrl,
                        players = match.Select(p => p.PlayerId).ToList(),
                        ticketId,
                        ttlSeconds = _matchmakingService.GetTtlSeconds()
                    });
                }

            _logger.LogInformation("Player queued and waiting: PlayerId={PlayerId}, LobbySize={LobbySize}", request.PlayerId, request.LobbySize);
            return Ok(new { message = "Waiting for match...", ticketId, ttlSeconds = _matchmakingService.GetTtlSeconds() });
            */
        }

        [HttpPost("dev/botgame")]
        public IActionResult CreateBotMatch([FromBody] PlayerJoinRequest request)
        {
            var ip = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation("BotGame hit: PlayerId={PlayerId}, QueueType={QueueType}, LobbySize={LobbySize}, IP={IP}",
                request?.PlayerId, request?.QueueType, request?.LobbySize, ip);

            var validationError = RequestValidator.ValidateJoinRequest(request);
            if (validationError != null)
            {
                _logger.LogWarning("BotGame validation failed for PlayerId={PlayerId}: {Result}", request?.PlayerId, validationError.GetType().Name);
                return validationError;
            }

            var matchSize = LobbySizeUtils.ToPlayerCount(request.LobbySize);
            var requestedBots = request.BotFill
                ? Math.Max(0, matchSize - 1)
                : Math.Min(Math.Max(0, request.BotCount ?? (matchSize - 1)), Math.Max(0, matchSize - 1));

            var match = new List<PlayerJoinRequest>
            {
                new PlayerJoinRequest
                {
                    PlayerId = request.PlayerId,
                    MMR = request.MMR,
                    LobbySize = request.LobbySize,
                    QueueType = request.QueueType,
                    BotFill = request.BotFill,
                    BotCount = request.BotCount
                }
            };

            for (int i = 0; i < requestedBots; i++)
            {
                match.Add(new PlayerJoinRequest
                {
                    PlayerId = $"BOT_{Guid.NewGuid().ToString().Substring(0, 5)}",
                    MMR = request.MMR,
                    LobbySize = request.LobbySize,
                    QueueType = request.QueueType
                });
            }

            var gameUrl = $"https://game-instance-{Guid.NewGuid().ToString().Substring(0, 8)}.alpha.com";

            if (match == null)
                return BadRequest("Match could not be created.");

            _logger.LogInformation("Dev bot match created: LobbySize={LobbySize}, Players={Players}", request.LobbySize, string.Join(",", match.Select(p => p.PlayerId)));
            return Ok(new
            {
                message = "Dev match with bots created.",
                gameUrl,
                players = match.Select(p => p.PlayerId).ToList(),
                botCount = requestedBots
            });
        }

        public class TicketRequest { public string TicketId { get; set; } = string.Empty; }

        [HttpPost("heartbeat")]
        public IActionResult Heartbeat([FromBody] TicketRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.TicketId)) return BadRequest("ticketId required");
            var ok = _matchmakingService.Heartbeat(req.TicketId);
            return Ok(ok);
        }

        [HttpPost("leave")]
        public IActionResult Leave([FromBody] TicketRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.TicketId)) return BadRequest("ticketId required");
            var ok = _matchmakingService.Leave(req.TicketId);
            if (!ok) return NotFound(new { message = "ticket not found" });
            _logger.LogInformation("Player left queue: TicketId={TicketId}", req.TicketId);
            return Ok(new { message = "left queue" });
        }

        [HttpGet("dev/state")]
        public IActionResult GetState()
        {
            var snapshot = _matchmakingService.GetQueueSnapshot();

            var response = new
            {
                generatedAtUtc = DateTime.UtcNow,
                queues = snapshot.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => new
                    {
                        count = kvp.Value.Count,
                        players = kvp.Value.Select(p => new
                        {
                            p.PlayerId,
                            p.MMR,
                            p.QueueType,
                            p.LobbySize,
                            p.BotFill,
                            p.BotCount,
                            p.TicketId,
                            p.Status,
                            p.EnqueuedAtUtc,
                            p.LastSeenAtUtc
                        }).ToList()
                    })
            };

            _logger.LogInformation("Dev state requested. Totals: Small={Small}, Medium={Medium}, Large={Large}",
                response.queues.TryGetValue("Small", out var small) ? small.count : 0,
                response.queues.TryGetValue("Medium", out var medium) ? medium.count : 0,
                response.queues.TryGetValue("Large", out var large) ? large.count : 0);

            return Ok(response);
        }
    }
}
