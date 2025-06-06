using System.Collections;

namespace Server
{
    public class Room : IEnumerable<Player>
    {
        public string ID { get; }
        public string SecretKey { get; }
        public List<Player> Players { get; } = new List<Player>();
        public int PlayerCount => Players.Count;
        public int MaxPlayers; // Maximum players in a room
        public bool IsFull => PlayerCount >= MaxPlayers;
        public bool Locked { get; set; } = false;
        public List<Player> ReconnectedPlayers = new List<Player>();

        public TimeSpan timeEmpty = TimeSpan.Zero;

        public Room(int MaxPlayers)
        {
            this.MaxPlayers = MaxPlayers;

            do { ID = RoomManager.GetUniqueRandomRoomId(); } while (RoomManager.Rooms.ContainsKey(ID));
            SecretKey = Guid.NewGuid().ToString(); // Generate a unique secret key for the room

            RoomManager.Rooms[ID] = this;
        }
        public bool AddPlayer(Player player)
        {
            if(player.PlayerName == null || player.PlayerName.Trim() == string.Empty)
            {
                player.ShowText("Player name cannot be nothing.");
                return false;
            }

            //check that the player is not already in the room
            if (GetPlayerByName(player.PlayerName, out Player? existingPlayer))
            {
                if (existingPlayer?.IsConnectionAlive() ?? false)
                    player.ShowText($"A user by that name is already in room {ID}.");
                else
                {
                    existingPlayer.OverrideWithPlayer(player);
                    //Players.Remove(player);

                    existingPlayer.SendPlayerID();
                    existingPlayer.ShowText($"Welcome back {player.PlayerName}, you joined room {ID}");
                    existingPlayer.SetImage("");
                    existingPlayer.OnReconnected();
                }
                return false;
            }

            if (IsFull)
            {
                player.ShowText("Room is full.");
                return false;
            }

            if (Locked)
            {
                player.ShowText("Room is locked.");
                return false;
            }

            Players.Add(player);
            player.ShowText($"Welcome {player.PlayerName}, you joined room {ID}.");
            player.SetImage("");
            return true;
        }



        public bool TechnicallyEmpty() => Players.Count == 0 || Players.All(p => !p.IsConnectionAlive());

        public bool GetPlayerByName(string playerName, out Player? player)
        {
            player = Players.FirstOrDefault(p => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            return player != null;
        }

        public IEnumerator<Player> GetEnumerator()
        {
            foreach (Player player in Players)
            {
                yield return player;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void InformOfReconnect(Player player)
        {
            ReconnectedPlayers.Add(player);
        }

        public Player this[string playerId]
        {
            get
            {
                return Players.FirstOrDefault(p => p.PlayerId == playerId) ?? throw new KeyNotFoundException($"Player with ID {playerId} not found in room {ID}.");
            }
        }
    }
}
