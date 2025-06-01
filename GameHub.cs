using Microsoft.AspNetCore.SignalR;
using Server;

public class GameHub : Hub
{

    public static Dictionary<string, Player> Connections = new Dictionary<string, Player>();

    public static void RemovePlayer(Player player)
    {
        if (Connections.ContainsKey(player.ConnectionId))
            Connections.Remove(player.ConnectionId);
    }

    public Player? GetPlayerFromPlayerId(string playerId) => Connections.Values.FirstOrDefault(p => p.PlayerId == playerId);

    public override Task OnConnectedAsync()
    {
        string connectionId = Context.ConnectionId;

        Player player = new Player(Clients.Client(connectionId), connectionId);
        Connections[connectionId] = player;

        return player.SendPlayerID();//Clients.Client(connectionId).SendAsync("ReceivePlayerId", player.PlayerId);
    }

    public Task SubmitAnswer(string playerId, string answer)
    {
        Player? player = GetPlayerFromPlayerId(playerId);
        if (player == null)
            return Task.CompletedTask;

        player.ReceivedAnswer(answer);
        
        return Task.CompletedTask;
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        if (Connections.TryGetValue(connectionId, out Player? player))
            player.Disconnected();

        return base.OnDisconnectedAsync(exception);
    }
}
