namespace AIRMatchmakingServer.Constants
{
    public static class LobbySizeLimits
    {
        public const int Small = 8;
        public const int Medium = 32;
        public const int Large = 100;

        public static readonly int[] AllSizes = { Small, Medium, Large };
    }
}