using MatimaticServer;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var hub = new GameHub();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.Map("/matematico/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    await hub.HandleConnection(socket);
});

app.Run();