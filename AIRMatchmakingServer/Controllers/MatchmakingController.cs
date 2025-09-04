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

        public MatchmakingController(MatchmakingService service)
        {
            _matchmakingService = service;
        }

        [HttpPost("join")]
        public IActionResult JoinQueue([FromBody] PlayerJoinRequest request)
        {
            var validationError = RequestValidator.ValidateJoinRequest(request);
            if (validationError != null)
                return validationError;

            if (_matchmakingService.TryAddPlayer(request, out var match))
                {
                    // Simulate spawning a game server and returning its URL
                    var gameUrl = $"https://game-instance-{Guid.NewGuid().ToString().Substring(0, 8)}.alpha.com";

                    if (match == null)
                        return BadRequest("Match could not be created.");

                    // In future: send all players to gameUrl
                    return Ok(new
                    {
                        message = "Match found!",
                        gameUrl,
                        players = match.Select(p => p.PlayerId).ToList()
                    });
                }

            return Ok(new { message = "Waiting for match..." });
        }

        [HttpPost("dev/botgame")]
        public IActionResult CreateBotMatch([FromBody] PlayerJoinRequest request)
        {
            var validationError = RequestValidator.ValidateJoinRequest(request);
            if (validationError != null)
                return validationError;

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

            return Ok(new
            {
                message = "Dev match with bots created.",
                gameUrl,
                players = match.Select(p => p.PlayerId).ToList()
            });
        }
    }
}
