namespace DrawfulTelegramBot;

internal class Room
{
    public const int MAX_PLAYER_COUNT = 8;

    public readonly int id;
    public readonly List<Player> playerList = new();

    public Player owner;
    public RoomState state;

    private int roundIndex;


    private int drawIndex;
    public Player NextDrawingPlayer => playerList[drawIndex];
    public bool HasNextDrawingPlayer => drawIndex < playerList.Count - 1;
    public IEnumerable<Player> VotingPlayers => playerList.Where(p => p != NextDrawingPlayer);

    public Room() {
        id = RoomIdPool.GetNewId();
        state = RoomState.WaitingForPlayers;
    }

    public bool CanAddMore => playerList.Count < MAX_PLAYER_COUNT;

    public void AssignTasks() {
        drawIndex = 0;
        roundIndex++;
        playerList.Shuffle();
        playerList.ForEach(p => {
            p.ResetScore();
            p.drawingTask = new DrawingTask();
        });
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
