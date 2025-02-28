using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpGamesServer
{
    abstract class GamesFactory
    {
        public abstract IGame CreateGamefactory();
    }
}
