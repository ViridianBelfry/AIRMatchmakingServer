using Microsoft.AspNetCore.Mvc;
using AIRMatchmakingServer.Models;
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
        public IActionResult JoinQueue([FromBody] PlayerJoinRequest request)
        {
            var ip = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation("JoinQueue hit: PlayerId={PlayerId}, QueueType={QueueType}, LobbySize={LobbySize}, IP={IP}",
                request?.PlayerId, request?.QueueType, request?.LobbySize, ip);

            var validationError = RequestValidator.ValidateJoinRequest(request);
            if (validationError != null)
            {
                _logger.LogWarning("JoinQueue validation failed for PlayerId={PlayerId}: {Result}", request?.PlayerId, validationError.GetType().Name);
                return validationError;
            }

            if (_matchmakingService.TryAddPlayer(request, out var match))
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
                        players = match.Select(p => p.PlayerId).ToList()
                    });
                }

            _logger.LogInformation("Player queued and waiting: PlayerId={PlayerId}, LobbySize={LobbySize}", request.PlayerId, request.LobbySize);
            return Ok(new { message = "Waiting for match..." });
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
            var match = new List<PlayerJoinRequest>
            {
                new PlayerJoinRequest
                {
                    PlayerId = request.PlayerId,
                    MMR = request.MMR,
                    LobbySize = request.LobbySize
                }
            };

            for (int i = 1; i < matchSize; i++)
            {
                match.Add(new PlayerJoinRequest
                {
                    PlayerId = $"BOT_{Guid.NewGuid().ToString().Substring(0, 5)}",
                    MMR = request.MMR,
                    LobbySize = request.LobbySize
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
                players = match.Select(p => p.PlayerId).ToList()
            });
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
                            p.LobbySize
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
