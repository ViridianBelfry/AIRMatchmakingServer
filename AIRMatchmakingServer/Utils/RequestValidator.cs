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
    }
}
