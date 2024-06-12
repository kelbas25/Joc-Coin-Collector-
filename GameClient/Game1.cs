using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GameClient
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Texture2D _playerTexture;
        private Texture2D _coinTexture;
        private Vector2 _playerPosition;
        private Dictionary<int, Vector2> _otherPlayerPositions = new Dictionary<int, Vector2>();

        private TcpClient _client;
        private NetworkStream _stream;
        private List<Vector2> _coinPositions = new List<Vector2>();
        private readonly object _coinPositionsLock = new object(); // Объект блокировки для синхронизации доступа
        private readonly object _otherPlayerPositionsLock = new object(); // Объект блокировки для синхронизации доступа к позициям других игроков
        private int _clientId; // Идентификатор текущего клиента

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _playerPosition = new Vector2(100, 100);
            ConnectToServer();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _playerTexture = Content.Load<Texture2D>("player");
            _coinTexture = Content.Load<Texture2D>("coin");
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            var keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.W))
                _playerPosition.Y -= 1;
            if (keyboardState.IsKeyDown(Keys.S))
                _playerPosition.Y += 1;
            if (keyboardState.IsKeyDown(Keys.A))
                _playerPosition.X -= 1;
            if (keyboardState.IsKeyDown(Keys.D))
                _playerPosition.X += 1;

            SendPlayerPosition();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _spriteBatch.Begin();
            _spriteBatch.Draw(_playerTexture, _playerPosition, Color.White);

            lock (_coinPositionsLock)
            {
                // Отрисовка монеток
                foreach (var coinPosition in _coinPositions)
                {
                    _spriteBatch.Draw(_coinTexture, coinPosition, Color.White);
                }
            }

            lock (_otherPlayerPositionsLock)
            {
                foreach (var kvp in _otherPlayerPositions)
                {
                    _spriteBatch.Draw(_playerTexture, kvp.Value, Color.Red);
                }
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private async void ConnectToServer()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync("127.0.0.1", 5000);
                _stream = _client.GetStream();
                Console.WriteLine("Connected to server.");
                _ = ReceiveDataAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while connecting to server: {ex.Message}");
            }
        }

        private async Task ReceiveDataAsync()
        {
            byte[] buffer = new byte[2048]; // Увеличен размер буфера для приема данных
            int byteCount;

            while ((byteCount = await _stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                string response = Encoding.ASCII.GetString(buffer, 0, byteCount);
                Console.WriteLine($"Received: {response}");

                var parts = response.Split('\n');

                // Обработка идентификатора клиента
                if (parts.Length > 0 && parts[0].StartsWith("clientid:"))
                {
                    _clientId = int.Parse(parts[0].Substring(9));
                    continue;
                }

                // Обработка сообщений о позициях игроков
                if (parts.Length > 1 && parts[0].StartsWith("players:"))
                {
                    var playerData = parts[0].Substring(8);
                    var playerPositions = playerData.Split(';');
                    
                    lock (_otherPlayerPositionsLock)
                    {
                        _otherPlayerPositions.Clear();

                        foreach (var playerPos in playerPositions)
                        {
                            if (!string.IsNullOrEmpty(playerPos))
                            {
                                var coords = playerPos.Split(',');
                                if (coords.Length == 3 && int.TryParse(coords[0], out int playerId) && float.TryParse(coords[1], out float x) && float.TryParse(coords[2], out float y))
                                {
                                    if (playerId != _clientId)
                                    {
                                        _otherPlayerPositions[playerId] = new Vector2(x, y);
                                    }
                                }
                            }
                        }
                    }
                }

                // Обработка сообщений с координатами всех монеток
                if (parts.Length > 1 && parts[1].StartsWith("coins:"))
                {
                    lock (_coinPositionsLock)
                    {
                        _coinPositions.Clear();
                        var coinData = parts[1].Substring(6);
                        var coinPositions = coinData.Split(';');
                        foreach (var coinPos in coinPositions)
                        {
                            if (!string.IsNullOrEmpty(coinPos))
                            {
                                var coords = coinPos.Split(',');
                                if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                                {
                                    _coinPositions.Add(new Vector2(x, y));
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void SendPlayerPosition()
        {
            if (_stream != null)
            {
                string message = $"{_playerPosition.X},{_playerPosition.Y}";
                byte[] messageBytes = Encoding.ASCII.GetBytes(message);
                await _stream.WriteAsync(messageBytes, 0, messageBytes.Length);
            }
        }
    }
}
