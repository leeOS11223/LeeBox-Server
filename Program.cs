using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Server;
using Microsoft.AspNetCore.Identity;
using Swashbuckle.AspNetCore.Swagger;

public class Program
{
    public static event Action? UpdateEvent;
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add SignalR services
        builder.Services.AddControllers();
        builder.Services.AddSignalR();

        //builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        // Serve index.html and static files
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Map the SignalR hub
        app.MapHub<GameHub>("/gamehub");

        app.MapControllers();

        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);
                UpdateEvent?.Invoke();
            }
        });
        RoomManager.Initialize();

        app.Run();
    }



}