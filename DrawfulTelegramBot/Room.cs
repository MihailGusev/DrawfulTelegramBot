namespace DrawfulTelegramBot;

internal class Room
{
    public const int MAX_PLAYER_COUNT = 8;

    public readonly int id;
    public readonly List<Player> playerList = new();

    public Player owner;
    public RoomState state;

    private int roundIndex = 1;
    private int roundCount;

    private int drawIndex;
    public Player NextDrawingPlayer => playerList[drawIndex];
    public bool HasNextDrawingPlayer
    {
        get {
            return drawIndex < playerList.Count - 1;
        }
    }

    public IEnumerable<Player> VotingPlayers => playerList.Where(p => p != NextDrawingPlayer);

    public Room() {
        id = RoomIdPool.GetNewId();
        state = RoomState.WaitingForPlayers;
    }

    public bool CanAddPlayer => playerList.Count < MAX_PLAYER_COUNT;

    public void Add

    public int PrepareForNewGame() {
        roundCount = playerList.Count > 5 ? 1 : 2;
        for (var i = 0; i < playerList.Count; i++) {
            playerList[i]
        }
        PrepareForNewRound();
        return roundCount;
    }

    public int PrepareForNewRound() {
        state = RoomState.Drawing;
        drawIndex = 0;
        playerList.Shuffle();
        playerList.ForEach(p => p.drawingTask = new DrawingTask());
        if (roundIndex == roundCount) {
            roundIndex = 1;
            playerList.ForEach(p => p.ResetScore());
        }
        else {
            roundIndex++;
        }
        return roundIndex;
    }

    public void MoveToDrawingState() {
        state = RoomState.Drawing;
    }

    public void MoveToGuessingState(bool moveIndex = false) {
        state = RoomState.Guessing;
        if (moveIndex) {
            drawIndex++;
        }
    }

    public void MoveToVotingState() {
        state = RoomState.Voting;
    }

    public void MoveToShowingResultsState() {
        state = RoomState.ShowingResults;
    }

    public void MoveToFinishedState() {
        state = RoomState.Finished;
    }

    public void Close() {
        RoomIdPool.ReleaseId(id);
    }
}
