namespace DrawfulTelegramBot
{
    internal class Room
    {
        public readonly string id;
        public readonly List<Player> playerList = new();
        public RoomState roomState;

        public Room() {
            roomState = RoomState.WaitingForPlayers;
            id = RoomIdPool.GetNewId();
        }
    }
}
