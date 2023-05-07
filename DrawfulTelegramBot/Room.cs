namespace DrawfulTelegramBot;

internal class Room
{
    public readonly int id;
    public readonly List<Player> playerList = new();

    public Player host;
    public RoomState roomState;

    private int drawIndex;
    public Player PlayerToGuess => playerList[drawIndex];
    public bool HasNextPlayerToGuess => drawIndex < playerList.Count - 1;

    public Room() {
        id = RoomIdPool.GetNewId();
        roomState = RoomState.WaitingForPlayers;
    }

    public void MoveToDrawingState() {
        roomState = RoomState.Drawing;
    }

    public Player MoveToGuessingState() {
        roomState = RoomState.Guessing;
        return playerList[drawIndex++];
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
