namespace DrawfulTelegramBot;

internal class Room
{
    public readonly int id;
    public readonly List<Player> playerList = new();

    public Player owner;
    public RoomState roomState;

    private int drawIndex;

    public Player PlayerToGuess => playerList[drawIndex];
    public bool HasNextPlayerToGuess => drawIndex < playerList.Count - 1;
    public IEnumerable<Player> VotingPlayers => playerList.Where(p => p != PlayerToGuess);

    public Room() {
        id = RoomIdPool.GetNewId();
        roomState = RoomState.WaitingForPlayers;
    }

    public void AssignTasks() {
        drawIndex = 0;
        playerList.Shuffle();
        playerList.ForEach(p => {
            p.ResetScore();
            p.drawingTask = new DrawingTask();
        });
    }

    public void MoveToDrawingState() {
        roomState = RoomState.Drawing;
    }

    public void MoveToGuessingState(bool moveIndex = false) {
        roomState = RoomState.Guessing;
        if (moveIndex) {
            drawIndex++;
        }
    }

    public void MoveToVotingState() {
        roomState = RoomState.Voting;
    }

    public void MoveToShowingResultsState() {
        roomState = RoomState.ShowingResults;
    }

    public void MoveToFinishedState() {
        roomState = RoomState.Finished;
    }

    public void Close() {
        RoomIdPool.ReleaseId(id);
    }
}
