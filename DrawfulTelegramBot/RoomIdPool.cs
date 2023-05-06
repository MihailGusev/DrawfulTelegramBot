namespace DrawfulTelegramBot
{
    internal static class RoomIdPool
    {
        private const int IdLength = 2;
        private static readonly HashSet<string> pool = new();

        public static string GetNewId() {
            var chars = "abcdefghijklmnopqrstuvwxyz";
            var stringChars = new char[IdLength];
            var random = new Random();

            string result;
            do {
                for (int i = 0; i < stringChars.Length; i++) {
                    stringChars[i] = chars[random.Next(chars.Length)];
                }
                result = new(stringChars);
            }
            while (!pool.Add(result));

            return result;
        }

        public static void DeleteId(string id) => pool.Remove(id);
    }
}
