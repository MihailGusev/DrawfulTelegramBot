namespace DrawfulTelegramBot;

internal class Room
{
    private const int MAX_PLAYER_COUNT = 8;

    public readonly int id;
    public readonly List<Player> playerList = new();
    public bool CanAddPlayer => playerList.Count < MAX_PLAYER_COUNT;

    public Player owner;
    public RoomState state;

    // Индекс игрока, рисунок которого сейчас отгадывается
    private int playerBeingGuessedIndex;
    public Player BeingGuessedPlayer => playerList[playerBeingGuessedIndex];
    public bool HasNextBeingGuessedPlayer => playerBeingGuessedIndex < playerList.Count - 1;
    public IEnumerable<Player> VotingPlayers => playerList.Where(p => p != BeingGuessedPlayer);

    public int RoundIndex { get; private set; } = 1;
    public int RoundCount => playerList.Count > 5 ? 1 : 2;
    public bool HasMoreRounds => RoundIndex < RoundCount;

    public Room() {
        id = RoomIdPool.GetNewId();
        state = RoomState.WaitingForPlayers;
    }

    public void AssignTasks() {
        playerBeingGuessedIndex = 0;
        playerList.Shuffle();
        playerList.ForEach(p => p.drawingTask = new DrawingTask());
    }

    public void PrepareForNewRound() {
        AssignTasks();
        RoundIndex++;
    }

    public void Reset() {
        playerList.ForEach(p => p.ResetScore());
        RoundIndex = 1;
    }

    public void MoveToDrawingState() {
        state = RoomState.Drawing;
    }

    public void MoveToGuessingState(bool moveIndex = false) {
        state = RoomState.Guessing;
        if (moveIndex) {
            playerBeingGuessedIndex++;
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
