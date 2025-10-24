using Microsoft.AspNetCore.Mvc;
using AIRMatchmakingServer.Models;

namespace AIRMatchmakingServer.Utils
{
    public static class RequestValidator
    {
        public static IActionResult? ValidateJoinRequest(PlayerJoinRequest request)
        {
            var sizeError = ValidateLobbySize(request.LobbySize);
            if (sizeError != null) return sizeError;

            var rankedError = ValidateRankedLobbyConstraint(request.QueueType, request.LobbySize);
            if (rankedError != null) return rankedError;

            var botError = ValidateBotRequest(request);
            if (botError != null) return botError;

            return null;
        }

        public static IActionResult? ValidateLobbySize(LobbySize size)
        {
            if (!Enum.IsDefined(typeof(LobbySize), size))
            {
                return new BadRequestObjectResult($"Invalid lobby size. Must be Small ({LobbySizeUtils.ToPlayerCount(LobbySize.Small)}), Medium ({LobbySizeUtils.ToPlayerCount(LobbySize.Medium)}), or Large ({LobbySizeUtils.ToPlayerCount(LobbySize.Large)}).");
            }

            return null;
        }

        public static IActionResult? ValidateRankedLobbyConstraint(QueueType queue, LobbySize size)
        {
            if (queue == QueueType.Ranked && size == LobbySize.Large)
            {
                return new BadRequestObjectResult($"Ranked queues only support Small ({LobbySizeUtils.ToPlayerCount(LobbySize.Small)}) or Medium ({LobbySizeUtils.ToPlayerCount(LobbySize.Medium)}) lobbies.");
            }

            return null;
        }

        public static IActionResult? ValidateBotRequest(PlayerJoinRequest request)
        {
            // Do not allow both BotFill and BotCount simultaneously to keep semantics simple
            if (request.BotFill && request.BotCount.HasValue && request.BotCount.Value > 0)
            {
                return new BadRequestObjectResult("Specify either botFill or botCount, not both.");
            }

            if (request.BotCount.HasValue)
            {
                if (request.BotCount.Value < 0)
                {
                    return new BadRequestObjectResult("botCount must be >= 0.");
                }

                var capacity = LobbySizeUtils.ToPlayerCount(request.LobbySize);
                if (request.BotCount.Value >= capacity)
                {
                    return new BadRequestObjectResult($"botCount must be less than lobby capacity ({capacity}).");
                }
            }

            return null;
        }
    }
}
