namespace DrawfulTelegramBot;

internal class Room
{
    public const int MAX_PLAYER_COUNT = 8;

    public readonly int id;
    public readonly List<Player> playerList = new();
    public bool CanAddPlayer => playerList.Count < MAX_PLAYER_COUNT;

    public Player owner;
    public RoomState state;

    public int RoundIndex { get; private set; } = 1;
    public int RoundCount => playerList.Count > 5 ? 1 : 2;
    public bool HasMoreRounds => RoundIndex < RoundCount;

    private int drawingPlayerIndex;
    public Player NextDrawingPlayer => playerList[drawingPlayerIndex];
    public bool HasNextDrawingPlayer => drawingPlayerIndex < playerList.Count - 1;

    public IEnumerable<Player> VotingPlayers => playerList.Where(p => p != NextDrawingPlayer);

    public Room() {
        id = RoomIdPool.GetNewId();
        state = RoomState.WaitingForPlayers;
    }


    public void PrepareForNewRound() {
        MoveToDrawingState();
        AssignTasks();
        if (RoundIndex == RoundCount) {
            RoundIndex = 1;
            playerList.ForEach(p => p.ResetScore());
        }
        else {
            RoundIndex++;
        }
    }

    private void AssignTasks() {
        drawingPlayerIndex = 0;
        playerList.Shuffle();
        playerList.ForEach(p => p.drawingTask = new DrawingTask());
    }

    public void MoveToDrawingState() {
        state = RoomState.Drawing;
    }

    public void MoveToGuessingState(bool moveIndex = false) {
        state = RoomState.Guessing;
        if (moveIndex) {
            drawingPlayerIndex++;
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
