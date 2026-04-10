using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Разрешаем вебсокеты
builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    serverOptions.ListenAnyIP(8080); // Railway будет пробрасывать порт
});

var app = builder.Build();

// Хранилище подключений: RoomId -> (ClientId -> ClientInfo)
var rooms = new ConcurrentDictionary<string, ConcurrentDictionary<string, ClientInfo>>();

app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    // Получаем ID комнаты из URL: wss://site/ws?room=call_123
    var roomId = context.Request.Query["room"].ToString();
    if (string.IsNullOrEmpty(roomId)) return;

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var clientId = Guid.NewGuid().ToString(); // Уникальный ID текущего подключения

    // Добавляем в комнату
    var clientInfo = new ClientInfo(clientId, socket);
    var room = rooms.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, ClientInfo>());
    room[clientId] = clientInfo;

    try
    {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // Remote party closed without a proper close handshake — treat as disconnect
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close) break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            
            // Отправляем сообщение ВСЕМ в комнате, кроме отправителя
            if (rooms.TryGetValue(roomId, out var clients))
            {
                var sendTasks = clients.Values
                    .Where(c => c.Id != clientId && c.Socket.State == WebSocketState.Open)
                    .Select(c => c.Socket.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None));
                await Task.WhenAll(sendTasks);
            }
        }
    }
    finally
    {
        // Чистка при отключении
        if (rooms.TryGetValue(roomId, out var clients))
        {
            clients.TryRemove(clientId, out _);
            if (clients.IsEmpty) rooms.TryRemove(roomId, out _);
        }
    }
});

app.MapGet("/ping", () => "ok");

app.Run();

record ClientInfo(string Id, WebSocket Socket);