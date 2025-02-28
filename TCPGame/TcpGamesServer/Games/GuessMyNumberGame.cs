using System.Net.Sockets;
using TcpGamesServer;

internal class GuessMyNumberGame : IGame
{
    public string Name => throw new NotImplementedException();

    public int RequiredPlayers => throw new NotImplementedException();

    public GuessMyNumberGame(GamesServer gamesServer)
    {

    }

    public bool AddPlayer(TcpClient player)
    {
        throw new NotImplementedException();
    }

    public void DisconnectClient(TcpClient client)
    {
        throw new NotImplementedException();
    }

    public void Run()
    {
        throw new NotImplementedException();
    }
}