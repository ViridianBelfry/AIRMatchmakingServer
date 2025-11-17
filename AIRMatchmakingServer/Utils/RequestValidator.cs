namespace AIRMatchmakingServer.Utils
{
    public static class RequestValidator
    {
        public static string? ValidateJoinRequest(PlayerJoinRequest request)
        {
            if (request == null)
            {
                return "Request body is required.";
            }

            if (string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return "playerId is required.";
            }

            var sizeError = ValidateLobbySize(request.LobbySize);
            if (sizeError != null) return sizeError;

            var rankedError = ValidateRankedLobbyConstraint(request.QueueType, request.LobbySize);
            if (rankedError != null) return rankedError;

            var botError = ValidateBotRequest(request);
            if (botError != null) return botError;

            return null;
        }

        public static string? ValidateLobbySize(LobbySize size)
        {
            if (!Enum.IsDefined(typeof(LobbySize), size))
            {
                return $"Invalid lobby size. Must be Small ({LobbySizeUtils.ToPlayerCount(LobbySize.Small)}), Medium ({LobbySizeUtils.ToPlayerCount(LobbySize.Medium)}), or Large ({LobbySizeUtils.ToPlayerCount(LobbySize.Large)}).";
            }

            return null;
        }

        public static string? ValidateRankedLobbyConstraint(QueueType queue, LobbySize size)
        {
            if (queue == QueueType.Ranked && size == LobbySize.Large)
            {
                return $"Ranked queues only support Small ({LobbySizeUtils.ToPlayerCount(LobbySize.Small)}) or Medium ({LobbySizeUtils.ToPlayerCount(LobbySize.Medium)}) lobbies.";
            }

            return null;
        }

        public static string? ValidateBotRequest(PlayerJoinRequest request)
        {
            // Do not allow both BotFill and BotCount simultaneously to keep semantics simple
            if (request.BotFill && request.BotCount.HasValue && request.BotCount.Value > 0)
            {
                return "Specify either botFill or botCount, not both.";
            }

            if (request.BotCount.HasValue)
            {
                if (request.BotCount.Value < 0)
                {
                    return "botCount must be >= 0.";
                }

                var capacity = LobbySizeUtils.ToPlayerCount(request.LobbySize);
                if (request.BotCount.Value >= capacity)
                {
                    return $"botCount must be less than lobby capacity ({capacity}).";
                }
            }

            return null;
        }
    }
}
