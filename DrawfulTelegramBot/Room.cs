namespace DrawfulTelegramBot;

internal class Room
{
    public readonly int id;
    private int drawIndex;
    public List<Player> playerList { get; private set; }
    public Player host;
    public RoomState RoomState { get; private set; }

    public Room() {
        RoomState = RoomState.WaitingForPlayers;
        id = RoomIdPool.GetNewId();
    }

    public void MoveToDrawingState() {
        playerList.Shuffle();
        foreach (var player in playerList) {
            player.task = new DrawingTask();
        }
        RoomState = RoomState.Drawing;
    }

    public Player MoveToGuessingState() {
        RoomState = RoomState.Guessing;
        return playerList[drawIndex++];
    }

    public IEnumerator<Player> GetNextPlayer() {
        foreach (var player in playerList) {
            yield return player;
        }
    }
}
