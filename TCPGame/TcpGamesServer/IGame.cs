using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpGamesServer
{
    interface IGame
    {
        #region Properties
        string Name { get; }
        int RequiredPlayers { get; }
        #endregion

        #region Fuctions
        bool AddPlayer(TcpClient player);

        void DisconnectClient(TcpClient client);

        void Run();
        #endregion
    }
}
