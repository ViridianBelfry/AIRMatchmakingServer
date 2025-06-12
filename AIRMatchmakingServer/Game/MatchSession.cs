namespace AIRMatchmakingServer.Game
{
    public class MatchSession
    {
        public Guid MatchId { get; }

        public MatchSession(Guid id)
        {
            MatchId = id;
        }

        // Later we'll add GameState and simulation logic here
    }
}
