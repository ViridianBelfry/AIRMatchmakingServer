using AIRMatchmakingServer.Models;
using AIRMatchmakingServer.Constants;

namespace AIRMatchmakingServer.Utils
{
    public static class LobbySizeUtils
    {
        public static int ToPlayerCount(LobbySize size)
        {
            return size switch
            {
                LobbySize.Small => LobbySizeLimits.Small,
                LobbySize.Medium => LobbySizeLimits.Medium,
                LobbySize.Large => LobbySizeLimits.Large,
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unsupported lobby size")
            };
        }
    }
}