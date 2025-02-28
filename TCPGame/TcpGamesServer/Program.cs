string name = "Bad BBS";//args[0];
int port = 6000;//int.Parse(args[1]);

GamesServer gamesServer = new GamesServer(name, port);

// Handler for Ctrl-C presses
Console.CancelKeyPress += gamesServer.InterruptHandler;

// Create and run the server
gamesServer.Run();