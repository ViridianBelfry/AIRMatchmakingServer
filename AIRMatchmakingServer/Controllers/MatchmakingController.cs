using Microsoft.AspNetCore.Mvc;
using AIRMatchmakingServer.Models;
using AIRMatchmakingServer.Services;

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
            if (_matchmakingService.TryAddPlayer(request, out var match))
            {
                // Simulate spawning a game server and returning its URL
                var gameUrl = $"https://game-instance-{Guid.NewGuid().ToString().Substring(0, 8)}.alpha.com";

                // In future: send all players to gameUrl
                return Ok(new
                {
                    message = "Match created!",
                    gameUrl,
                    players = match.Select(p => p.PlayerId).ToList()
                });
            }

            return Ok(new { message = "Waiting for match..." });
        }
    }
}
