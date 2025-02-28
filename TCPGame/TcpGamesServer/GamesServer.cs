
using System.Net.Sockets;
using System.Net;
using System.Xml.Linq;
using TcpGamesServer;
using TcpGameSharedUtility;
using System.Text;

internal class GamesServer
{
    private TcpListener _listener;

    // Clients objects
    private List<TcpClient> _clients = new List<TcpClient>();
    private List<TcpClient> _waitingLobby = new List<TcpClient>();

    // Game stuff
    private Dictionary<TcpClient, IGame> _gameClientIsIn = new Dictionary<TcpClient, IGame>();
    private List<IGame> _games = new List<IGame>();
    private List<Thread> _gameThreads = new List<Thread>();
    private IGame? _nextGame;

    // Other data
    public readonly string Name;
    public readonly int Port;
    public bool Running { get; private set; }


    public GamesServer(string name, int port)
    {
        Name = name;
        Port = port;
        Running = false;

        _listener = new TcpListener(IPAddress.Any, Port);

    }

    public void Shutdown()
    {
        if (Running)
        {
            Running = false;
            Console.WriteLine($"Shutting down {Name} server...");
        }
    }

    internal void InterruptHandler(object? sender, ConsoleCancelEventArgs e)
    {
        throw new NotImplementedException();
    }

    internal void Run()
    {
        Console.WriteLine($"Starting the \"{Name}\" Game(s) Server on port {Port}.");
        Console.WriteLine("Press Ctrl-C to shutdown the server at any time.");

        // Start the next game
        _nextGame = new GuessMyNumberGame(this);

        // Start running the server
        _listener.Start();
        Running = true;
        List<Task> newConnectionTasks = new List<Task>();
        Console.WriteLine("Waiting for incommming connections...");

        while (Running)
        {
            // Handle any new clients
            if (_listener.Pending())
                newConnectionTasks.Add(CheckForNewClients());

            // Once we have enough clients for the next game, add them in and start the game
            HandleLobby();

            // Check if any clients have disconnected in waiting, gracefully or not
            // NOTE: This could (and should) be parallelized
            CheckForDisconnects();


            // Take a small nap
            Thread.Sleep(10);
        }
        // In the chance a client connected but we exited the loop, give them 1 second to finish
        Task.WaitAll(newConnectionTasks.ToArray(), 1000);

        // Shutdown all of the threads, regardless if they are done or not
        foreach (Thread thread in _gameThreads)
            thread.Abort();

        // Disconnect any clients still here
        Parallel.ForEach(_clients, (client) =>
        {
            DisconnectClient(client, "The Game(s) Server is being shutdown.");
        });

        // Cleanup our resources
        _listener.Stop();

        // Info
        Console.WriteLine("The server has been shut down.");

    }

    private void DisconnectClient(TcpClient client, string message = "")
    {
        Console.WriteLine("Disconnecting the client from {0}.", client.Client.RemoteEndPoint);

        // If there wasn't a message set, use the default "Goodbye."
        if (message == "")
            message = "Goodbye.";

        // Send the "bye," message
        Task byePacket = SendPacket(client, new Packet(Command.bye, message));

        // Notify a game that might have them
        try
        {
            _gameClientIsIn[client]?.DisconnectClient(client);
        }
        catch (KeyNotFoundException) { }

        // Give the client some time to send and proccess the graceful disconnect
        Thread.Sleep(100);

        // Cleanup resources on our end
        byePacket.GetAwaiter().GetResult();
        HandleDisconnectedClient(client);
    }

    private void CheckForDisconnects()
    {
        foreach (TcpClient client in _waitingLobby.ToArray())
        {
            EndPoint? endPoint = client.Client.RemoteEndPoint;
            bool disconnected = false;

            // Check for graceful first
            Packet? p = ReceivePacket(client).GetAwaiter().GetResult();
            disconnected = (p?.Command == Command.bye);

            // Then ungraceful
            disconnected |= TCPSharedFunctions.IsClientDisconnected(client);

            if (disconnected)
            {
                HandleDisconnectedClient(client);
                Console.WriteLine($"Client {endPoint} has disconnected from the Game(s) Server.");
            }
        }
    }

    private void HandleDisconnectedClient(TcpClient client)
    {
        // Remove from collections and free resources
        _clients.Remove(client);
        _waitingLobby.Remove(client);
        TCPSharedFunctions.CleanupClient(client);
    }

    #region Packet Transmission Methods

    private async Task<Packet?> ReceivePacket(TcpClient client)
    {
        Packet? packet = null;
        try
        {
            // First check there is data available
            if (client.Available == 0)
                return null;

            NetworkStream msgStream = client.GetStream();

            // There must be some incoming data, the first two bytes are the size of the Packet
            byte[] lengthBuffer = new byte[2];
            await msgStream.ReadAsync(lengthBuffer, 0, 2);
            ushort packetByteSize = BitConverter.ToUInt16(lengthBuffer, 0);

            // Now read that many bytes from what's left in the stream, it must be the Packet
            byte[] jsonBuffer = new byte[packetByteSize];
            await msgStream.ReadAsync(jsonBuffer, 0, jsonBuffer.Length);

            // Convert it into a packet datatype
            string jsonString = Encoding.UTF8.GetString(jsonBuffer);
            packet = Packet.FromJson(jsonString);

            //Console.WriteLine("[RECEIVED]\n{0}", packet);
        }
        catch (Exception e)
        {
            // There was an issue in receiving
            Console.WriteLine($"There was an issue sending a packet to {client.Client.RemoteEndPoint}.");
            Console.WriteLine($"Reason: {e.Message}");
        }

        return packet;
    }
    private async Task SendPacket(TcpClient client, Packet packet)
    {
        try
        {
            // convert JSON to buffer and its length to a 16 bit unsigned integer buffer
            byte[] jsonBuffer = Encoding.UTF8.GetBytes(packet.ToJson());
            byte[] lengthBuffer = BitConverter.GetBytes(Convert.ToUInt16(jsonBuffer.Length));

            // Join the buffers
            byte[] msgBuffer = new byte[lengthBuffer.Length + jsonBuffer.Length];
            lengthBuffer.CopyTo(msgBuffer, 0);
            jsonBuffer.CopyTo(msgBuffer, lengthBuffer.Length);

            // Send the packet
            await client.GetStream().WriteAsync(msgBuffer, 0, msgBuffer.Length);

            //Console.WriteLine("[SENT]\n{0}", packet);
        }
        catch (Exception e)
        {
            // There was an issue is sending
            Console.WriteLine("There was an issue receiving a packet.");
            Console.WriteLine("Reason: {0}", e.Message);
        }
    }
    #endregion

    private void HandleLobby()
    {
        if (_waitingLobby.Count >= _nextGame.RequiredPlayers)
        {
            // Get that many players from the waiting lobby and start the game
            int numPlayers = 0;
            while (numPlayers < _nextGame.RequiredPlayers)
            {
                // Pop the first one off
                TcpClient player = _waitingLobby[0];
                _waitingLobby.RemoveAt(0);

                // Try adding it to the game.  If failure, put it back in the lobby
                if (_nextGame.AddPlayer(player))
                    numPlayers++;
                else
                    _waitingLobby.Add(player);
            }

            // Start the game in a new thread!
            Console.WriteLine($"Starting a \"{_nextGame.Name}\" game.");
            Thread gameThread = new Thread(new ThreadStart(_nextGame.Run));
            gameThread.Start();
            _games.Add(_nextGame);
            _gameThreads.Add(gameThread);

            // Create a new game
            _nextGame = new GuessMyNumberGame(this);
        }
    }

    private async Task CheckForNewClients()
    {
        // Get the new client using a Future
        TcpClient newClient = await _listener.AcceptTcpClientAsync();
        Console.WriteLine($"New connection from {newClient.Client.RemoteEndPoint}.");

        // Store them and put them in the waiting lobby
        _clients.Add(newClient);
        _waitingLobby.Add(newClient);

        // Send a welcome message
        string msg = $"Welcome to the \"{Name}\" Games Server.\n";
        await SendPacket(newClient, new Packet(Command.message, msg));
    }

  
}
