using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

class Program
{
    private static List<(int x, int y)> coinPositions = new List<(int x, int y)>();
    private static Dictionary<int, (int x, int y)> playerPositions = new Dictionary<int, (int x, int y)>();
    private static int clientIdCounter = 0;

    static void Main(string[] args)
    {
        StartServer().GetAwaiter().GetResult();
    }

    static async Task StartServer()
    {
        TcpListener server = new TcpListener(IPAddress.Any, 5000);
        server.Start();
        Console.WriteLine("Server started on port 5000...");
        InitializeCoinPositions();
        var clients = new List<TcpClient>();

        while (true)
        {
            try
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                Console.WriteLine("Client connected.");
                clients.Add(client);
                int clientId = clientIdCounter++;
                playerPositions[clientId] = (100, 100); // начальная позиция

                // Отправка клиенту его идентификатора
                var clientIdData = Encoding.ASCII.GetBytes($"clientid:{clientId}\n");
                await client.GetStream().WriteAsync(clientIdData, 0, clientIdData.Length);

                _ = HandleClientAsync(client, clients, clientId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    static void InitializeCoinPositions()
    {
        int startX = 100;
        int startY = 310;
        int distance = 82; // (32 x 32) + 50 (distance between coins)

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                int x = startX + col * distance;
                int y = startY + row * 50;
                coinPositions.Add((x, y));
            }
        }
    }

    private static string GetAllCoinPositions()
    {
        var coinPositionsBuilder = new StringBuilder("coins:");
        foreach (var coinPosition in coinPositions)
        {
            coinPositionsBuilder.Append($"{coinPosition.x},{coinPosition.y};");
        }
        return coinPositionsBuilder.ToString();
    }

    private static string GetAllPlayerPositions()
    {
        var playerPositionsBuilder = new StringBuilder("players:");
        foreach (var playerPosition in playerPositions)
        {
            playerPositionsBuilder.Append($"{playerPosition.Key},{playerPosition.Value.x},{playerPosition.Value.y};");
        }
        return playerPositionsBuilder.ToString();
    }

    static async Task HandleClientAsync(TcpClient client, List<TcpClient> clients, int clientId)
    {
        try
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int byteCount;

            while ((byteCount = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                string request = Encoding.ASCII.GetString(buffer, 0, byteCount);
                Console.WriteLine($"Received from {clientId}: {request}");

                // Обновление позиции игрока
                var coords = request.Split(',');
                if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                {
                    playerPositions[clientId] = (x, y);
                }

                // Отправка данных о всех игроках и монетках всем клиентам
                var allPlayerPositions = GetAllPlayerPositions();
                var allCoinPositions = GetAllCoinPositions();
                var combinedData = $"{allPlayerPositions}\n{allCoinPositions}";
                var combinedDataBytes = Encoding.ASCII.GetBytes(combinedData);

                foreach (var c in clients)
                {
                    if (c.Connected)
                    {
                        await c.GetStream().WriteAsync(combinedDataBytes, 0, combinedDataBytes.Length);
                    }
                }
            }

            client.Close();
            clients.Remove(client);
            playerPositions.Remove(clientId);
            Console.WriteLine("Client disconnected.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while handling client {clientId}: {ex.Message}");
        }
    }
}
