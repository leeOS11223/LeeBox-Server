namespace Server
{
    public static class RoomManager
    {

        public static Dictionary<string, Room> Rooms = new Dictionary<string, Room>();

        public static void Initialize()
        {
            Program.UpdateEvent += Update;
        }
        static DateTime last = DateTime.Now;
        public static void Update()
        {
            List<string> emptyRooms = Rooms.Where(r => r.Value.TechnicallyEmpty()).Select(r => r.Key).ToList();
            foreach (string roomId in emptyRooms)
            {
                Room room = Rooms[roomId];
                room.timeEmpty += DateTime.Now - last;

                if(room.timeEmpty > TimeSpan.FromMinutes(15))
                    Rooms.Remove(roomId);
            }

            List<Room> fullRooms = Rooms.Where(r => !r.Value.TechnicallyEmpty()).Select(r => r.Value).ToList();
            foreach (Room room in fullRooms)
            {
                room.timeEmpty = TimeSpan.Zero; 
            }

            last = DateTime.Now;

            //Console.WriteLine(Rooms.Count);
        }

        public static string GetUniqueRandomRoomId()
        {
            // format 4 random letters/numbers
            Random random = new Random();
            string roomId = string.Empty;
            for (int i = 0; i < 4; i++)
            {
                int randomNumber = random.Next(0, 36); // 0-9 and a-z
                if (randomNumber < 10)
                {
                    roomId += randomNumber.ToString(); // 0-9
                }
                else
                {
                    roomId += ((char)(randomNumber - 10 + 'a')).ToString(); // a-z
                }
            }
            return roomId;
        }

        public static bool ContainsRoom(string roomId) => Rooms.ContainsKey(roomId);

        public static Room GetRoom(string roomId)
        {
            if (Rooms.TryGetValue(roomId, out Room? room))
            {
                return room;
            }
            throw new KeyNotFoundException($"Room with ID {roomId} not found.");
        }
    }
}
