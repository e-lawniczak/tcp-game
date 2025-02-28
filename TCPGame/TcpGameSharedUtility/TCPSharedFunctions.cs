using System;
using System.Net.Sockets;

namespace TcpGameSharedUtility
{
    public class TCPSharedFunctions
    {
        public static bool IsClientDisconnected(TcpClient aClinet)
        {
            try
            {
                Socket s = aClinet.Client;
                return s.Poll(10 * 1000, SelectMode.SelectRead) && (s.Available == 0);
            }
            catch (SocketException)
            {
                return true;
            }
        }
        public static void CleanupClient(TcpClient client, NetworkStream? stream = null)
        {
            client.GetStream().Close();
            client.Close();
            stream?.Close();
        }

    }
}


