using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.Annotations;

namespace Server
{
    [ApiController]
    [Route("api")]
    public class GameController : ControllerBase
    {
        //redirect to index.html
        [HttpGet("")]
        [SwaggerIgnore]
        public IActionResult Index()
        {
            return Redirect("swagger");
        }

        [HttpGet("newroom")]
        public IActionResult NewRoom([FromServices] IHubContext<GameHub> hub, [FromQuery] int maxPlayers = 100)
        {
            Room room = new Room(maxPlayers);
            return Ok(new {ID = room.ID, SecretKey = room.SecretKey});
        }

        [HttpGet("{roomId}")]
        public IActionResult GetRoom(string roomId)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);
            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            IActionResult reply =  Ok(new { ID = room.ID, PlayerCount = room.PlayerCount, 
                Players = room.Players, Locked = room.Locked, ReconnectedPlayers = room.ReconnectedPlayers.ToList() });

            room.ReconnectedPlayers.Clear(); // Clear reconnected players after sending the response

            return reply;
        }

        [HttpPost("{roomId}/broadcast")]
        public IActionResult Broadcast(string roomId, [FromBody] string message,
            [FromServices] IHubContext<GameHub> hub)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            foreach (Player player in room)
                player.CallSetAction((p) => p.ShowText(message));

            
            return Ok("Message sent");
        }

        [HttpPost("{roomId}/say/{userId}")]
        public IActionResult Broadcast(string roomId, string userId, [FromBody] string message,
            [FromServices] IHubContext<GameHub> hub)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();


            //room[userId]?.ShowText(message);
            room[userId]?.CallSetAction((p) => p.ShowText(message));

            return Ok("Message sent");
        }


        [HttpPost("{roomId}/setImage")]
        public IActionResult SetImage(string roomId, [FromBody] string message,
            [FromServices] IHubContext<GameHub> hub)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            foreach (Player player in room)
                player.CallSetAction((p) => p.SetImage(message));
            //player.SetImage(message);

            return Ok("Message sent");
        }

        [HttpPost("{roomId}/setImage/{userId}")]
        public IActionResult SetImage(string roomId, string userId, [FromBody] string message,
            [FromServices] IHubContext<GameHub> hub)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            //room[userId]?.SetImage(message);
            room[userId]?.CallSetAction((p) => p.SetImage(message));

            return Ok("Message sent");
        }

        [HttpPost("{roomId}/setlocked")]
        public async Task<IActionResult> SetLocked(string roomId, [FromBody] bool locked,
        [FromServices] IHubContext<GameHub> hub, [FromQuery] int timeoutSeconds = 30)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            room.Locked = locked;

            return Ok();
        }

        [HttpPost("{roomId}/ask")]
        public async Task<IActionResult> Ask(string roomId, [FromBody] string message,
            [FromServices] IHubContext<GameHub> hub, [FromQuery] int timeoutSeconds = 30)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            Dictionary<string, string> responses = new Dictionary<string, string>();

            DateTime targetTime = DateTime.Now.AddSeconds(timeoutSeconds);
            foreach (Player player in room)
                player.CallSetAction((p) =>
                    p.AskTextQuestion(message, 
                        (response) =>
                        {
                            if (DateTime.Now > targetTime.AddSeconds(2))
                            {
                                player.ClearSetAction();
                                player.ShowText($"Error - Was out of time.");
                                return;
                            }
                            responses.Add(player.PlayerId, (string)response); 
                            player.ShowText($"Your answer: {response}");
                        })
                    );
            //await for responses.Count == room.PlayerCount
            while (responses.Count < room.PlayerCount)
            {
                await Task.Delay(100); // wait for responses
                if (DateTime.Now > targetTime)
                    break;
            }
            foreach (Player player in room)
            {
                player.ClearSetAction();
                await player.ForceSubmit();
            }
            await Task.Delay(100); // wait for responses

            return Ok(responses);
        }

        [HttpPost("{roomId}/ask/{userId}")]
        public async Task<IActionResult> Ask(string roomId, string userId, [FromBody] string message,
            [FromServices] IHubContext<GameHub> hub, [FromQuery] int timeoutSeconds = 30)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            string? response = null;
            Player player = room[userId];

            DateTime targetTime = DateTime.Now.AddSeconds(timeoutSeconds);
            player.CallSetAction((p) =>
                p.AskTextQuestion(message,
                    (response2) =>
                    {
                        if (DateTime.Now > targetTime.AddSeconds(2))
                        {
                            player.ClearSetAction();
                            player.ShowText($"Error - Was out of time.");
                            return;
                        }
                        response = (string)response2;
                        player.ShowText($"Your answer: {response}");
                    })
                );

            //await for responses.Count == room.PlayerCount
            while (response == null)
            {
                await Task.Delay(100); // wait for responses
                if (DateTime.Now > targetTime)
                    break;
            }

            if (response == null)
            {
                player.ClearSetAction();
                await player.ForceSubmit();
            }
            await Task.Delay(100); // wait for responses

            return Ok(response);
        }


        public record OptionText(string message, string[] options, string?[] images);

        [HttpPost("{roomId}/options")]
        public async Task<IActionResult> Options(string roomId, [FromBody] OptionText data,
            [FromServices] IHubContext<GameHub> hub, [FromQuery] int timeoutSeconds = 30)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            if(data.options.Length <= 0)
                return BadRequest("Options must not be empty");

            Dictionary<string, string> responses = new Dictionary<string, string>();

            DateTime targetTime = DateTime.Now.AddSeconds(timeoutSeconds);
            foreach (Player player in room)
                player.CallSetAction((p) =>
                    p.OptionQuestion(data.message, data.options, data.images,
                        (response) =>
                        {
                            if (DateTime.Now > targetTime)
                            {
                                player.ClearSetAction();
                                player.ShowText($"Error - Was out of time.");
                                return;
                            }
                            responses.Add(player.PlayerId, (string)response);
                            player.ShowText($"Your answer: {response}");
                        })
                    );
            //await for responses.Count == room.PlayerCount
            while (responses.Count < room.PlayerCount)
            {
                await Task.Delay(100); // wait for responses
                if (DateTime.Now > targetTime)
                    break;
            }

            foreach (Player player in room)
            {
                player.ClearSetAction();
                if(!responses.ContainsKey(player.PlayerId))
                    player.ShowText("Out of time.");
            }

            return Ok(responses);
        }

        [HttpPost("{roomId}/options/{userId}")]
        public async Task<IActionResult> Options(string roomId, string userId, [FromBody] OptionText data,
            [FromServices] IHubContext<GameHub> hub, [FromQuery] int timeoutSeconds = 30)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            if (data.options.Length <= 0)
                return BadRequest("Options must not be empty");

            string? response = null;
            Player player = room[userId];

            DateTime targetTime = DateTime.Now.AddSeconds(timeoutSeconds);
            player.CallSetAction((p) =>
                p.OptionQuestion(data.message, data.options, data.images,
                    (response2) =>
                    {
                        if (DateTime.Now > targetTime)
                        {
                            player.ClearSetAction();
                            player.ShowText($"Error - Was out of time.");
                            return;
                        }
                        response = (string)response2;
                        player.ShowText($"Your answer: {response}");
                    })
                );

            //await for responses.Count == room.PlayerCount
            while (response == null)
            {
                await Task.Delay(100); // wait for responses
                if (DateTime.Now > targetTime)
                {
                    player.ShowText("Out of time.");
                    break;
                }
            }

            player.ClearSetAction();

            return Ok(response);
        }

        [HttpPost("{roomId}/draw")]
        public async Task<IActionResult> Draw(string roomId, [FromBody] string prompt,
            [FromServices] IHubContext<GameHub> hub, [FromQuery] int timeoutSeconds = 30)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            Dictionary<string, string> responses = new Dictionary<string, string>();

            DateTime targetTime = DateTime.Now.AddSeconds(timeoutSeconds);
            foreach (Player player in room)
                player.CallSetAction((p)=>
                    p.DrawQuestion(prompt,
                        (response) =>
                        {
                            if (DateTime.Now > targetTime.AddSeconds(2))
                            {
                                player.ClearSetAction();
                                player.ShowText($"Error - Was out of time.");
                                return;
                            }
                            responses.Add(player.PlayerId, (string)response);
                            player.ShowText($"Drawing received.");
                        })
                    );

            //await for responses.Count == room.PlayerCount
            while (responses.Count < room.PlayerCount)
            {
                await Task.Delay(100); // wait for responses
                if (DateTime.Now > targetTime)
                    break;
            }

            foreach (Player player in room)
            {
                player.ClearSetAction();
                await player.ForceSubmit();
            }
            await Task.Delay(100); // wait for responses

            return Ok(responses);
        }

        [HttpPost("{roomId}/draw/{userId}")]
        public async Task<IActionResult> Draw(string roomId, string userId, [FromBody] string prompt,
            [FromServices] IHubContext<GameHub> hub, [FromQuery] int timeoutSeconds=30)
        {
            if (!RoomManager.ContainsRoom(roomId))
                return NotFound("Room not found");

            Room room = RoomManager.GetRoom(roomId);

            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key != room.SecretKey)
                return Unauthorized();

            string? response = null;
            Player player = room[userId];

            DateTime targetTime = DateTime.Now.AddSeconds(timeoutSeconds);
            player.CallSetAction((p) =>
                    p.DrawQuestion(prompt,
                    (response2) =>
                    {
                        if (DateTime.Now > targetTime.AddSeconds(2))
                        {
                            player.ClearSetAction();
                            player.ShowText($"Error - Was out of time.");
                            return;
                        }
                        response = (string)response2;
                        player.ShowText($"Drawing received.");
                    })
                );

            //await for responses.Count == room.PlayerCount
            while (response == null)
            {
                await Task.Delay(100); // wait for responses
                if (DateTime.Now > targetTime)
                    break;
            }

            player.ClearSetAction();
            await player.ForceSubmit();
            await Task.Delay(100); // wait for responses

            return Ok(response);
        }
    }
}
