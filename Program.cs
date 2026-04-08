using MatimaticServer;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var sessionManager = new SessionManager();

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
    await sessionManager.HandleConnection(socket);
});

app.Run();