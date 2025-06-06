using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Numerics;
using System.Text.Json.Serialization;
using static System.Net.Mime.MediaTypeNames;

namespace Server
{
    public class Player
    {
        public string PlayerId { get; private set; }
        public string PlayerName { get; private set; } = "Not Set";

        [JsonIgnore]
        public ISingleClientProxy? Client { get; private set; }
        [JsonIgnore]
        public string ConnectionId { get; private set; }
        [JsonIgnore]
        private Stack<Action<object>> _answerCallbacks = new Stack<Action<object>>();
        [JsonIgnore]
        public Room? Room { get; private set; }

        private Action<Player>? lastAction = null;

        public Player(ISingleClientProxy client, string connectionId)
        {
            ConnectionId = connectionId;
            PlayerId = Guid.NewGuid().ToString();
            Client = client;

            AskTextQuestion("Room Code", 
                (code) => 
                {
                    string roomId = code.ToString().ToLower() ?? string.Empty;
                    if (RoomManager.ContainsRoom(roomId))
                    {
                        Room room = RoomManager.GetRoom(roomId);
                        AskTextQuestion("Name",
                            (name) =>
                            {
                                PlayerName = name.ToString().ToUpper() ?? "Unknown";
                                if (room.AddPlayer(this))
                                    Room = room;
                                else
                                    Remove();
                            });
                    }
                    else
                    {
                        ShowText($"Room {roomId} not found.");
                    }
                });

        }

        public void CallSetAction(Action<Player> action)
        {
            lastAction = action;
            action(this);
        }

        public void ClearSetAction()
        {
            lastAction = null;
        }

        public void OnReconnected()
        {
            Room.InformOfReconnect(this);
            lastAction?.Invoke(this);
        }

        public bool IsConnectionAlive()
        {
            return Client != null && !string.IsNullOrEmpty(ConnectionId);
        }

        public async void ShowText(string text)
        {
            text = WebUtility.HtmlEncode(text);
            if (Client == null || string.IsNullOrEmpty(ConnectionId))
                return; // Ensure the client is connected
            await Client.SendAsync("ShowText", text);
        }

        public async void AskTextQuestion(string text, Action<object> answer)
        {
            text = WebUtility.HtmlEncode(text);
            if (Client == null || string.IsNullOrEmpty(ConnectionId))
                return; // Ensure the client is connected
            _answerCallbacks.Push(answer);
            await Client.SendAsync("ShowTextbox", text);
        }
        public async void OptionQuestion(string message, string[] options, string?[] images, Action<object> answer)
        {
            message = WebUtility.HtmlEncode(message);
            options = options.Select(o => WebUtility.HtmlEncode(o)).ToArray();
            images = images.Select(i => i == null ? null : WebUtility.HtmlEncode(i)).ToArray();

            if (Client == null || string.IsNullOrEmpty(ConnectionId))
                return; // Ensure the client is connected
            _answerCallbacks.Push(answer);
            await Client.SendAsync("ShowOptions", message, options, images);
        }
        public async void DrawQuestion(string message, Action<object> answer)
        {
            message = WebUtility.HtmlEncode(message);

            if (Client == null || string.IsNullOrEmpty(ConnectionId))
                return; // Ensure the client is connected
            _answerCallbacks.Push(answer);
            await Client.SendAsync("ShowDrawbox", message);
        }
        public async void SetImage(string text)
        {
            text = WebUtility.HtmlEncode(text);

            if (Client == null || string.IsNullOrEmpty(ConnectionId))
                return; // Ensure the client is connected
            await Client.SendAsync("SetImage", text);
        }
        public async Task ForceSubmit()
        {
            if (Client == null || string.IsNullOrEmpty(ConnectionId))
                return; // Ensure the client is connected
            await Client.SendAsync("ForceSubmit");
        }

        public void ReceivedAnswer(string answer)
        {
            //Console.WriteLine($"Player {PlayerId} answered: {answer}");

            if (_answerCallbacks.Count != 0)
            {
                _answerCallbacks.Pop()(answer);
            }
            else
            {
                Console.WriteLine("No answer callback set.");
            }
        }

        public void OverrideWithPlayer(Player player)
        {
            this.ConnectionId = player.ConnectionId;
            this.Client = player.Client;
            //this.PlayerId = player.PlayerId;
            player.Remove();
            GameHub.Connections[ConnectionId] = this;

        }

        public void Remove()
        {
            GameHub.RemovePlayer(this);
            //Room?.Players.Remove(this);
            Client = null;
            ConnectionId = string.Empty;
        }

        public void Disconnected()
        {
            Console.WriteLine($"Player {PlayerId} disconnected.");
            ShowText("You have been disconnected from the room. Please refresh the page to reconnect.");

            //if(Room == null)
                Remove();
        }

        public Task SendPlayerID() => Client.SendAsync("ReceivePlayerId", PlayerId);
    }
}
