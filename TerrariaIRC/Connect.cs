using System;
using System.Threading;
using TShockAPI;

namespace TerrariaIRC
{
  public class Connect
  {
    private static bool _login    = false;

    // ConnectToIRC ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void ConnectToIRC()
    {
      bool joiningChat   = false;
      bool joiningAction = false;

      while ( TerrariaIRC._stayConnected && (TerrariaIRC._attemptCount < TerrariaIRC._maxAttempts) )
      {
        string server, port, botName;
        server  = TerrariaIRC._settings["server"];
        port    = TerrariaIRC._settings["port"];
        botName = TerrariaIRC._settings["botname"];

        try
        {
          if ( !TerrariaIRC._ircClient.IsConnected ) 
          {
            Log.Info( string.Format( "Connecting to {0}:{1}...", server, port ) );
            TerrariaIRC._ircClient.Connect( server, int.Parse( port ) );
            TerrariaIRC._ircClient.ListenOnce();
            Log.Info( "Connected to IRC server." );
          } // if
        } // try
        catch ( Exception exception )
        {
          Log.Error( string.Format( "Error connecting to IRC server {0}:{1} [attempt: {2}]", server, port, TerrariaIRC._attemptCount ) );
          Log.Error( exception.Message );
          if ( TerrariaIRC._stayConnected ) { Thread.Sleep( TerrariaIRC._sleepDelay ); }
          TerrariaIRC._attemptCount++;
        } // catch

        try
        {
          if ( !TerrariaIRC._loggedIn ) 
          {
            Log.Info( string.Format( "Trying to login as {0}...", botName ) );
            TerrariaIRC._ircClient.Login( botName, "TerrariaIRC" );
            TerrariaIRC._ircClient.ListenOnce();
            TerrariaIRC._loggedIn = true;
            Log.Info( string.Format( "Logged in as {0}.", botName ) );
          } // if
        } // try
        catch ( Exception exception )
        {
          Log.Error( string.Format( "Error logging on as {0} [attempt: {1}.", botName, TerrariaIRC._attemptCount ) );
          Log.Error( exception.Message );
          TerrariaIRC._attemptCount++;
        } // catch

        if ( TerrariaIRC._settings.ContainsKey( "nickserv" ) && TerrariaIRC._settings.ContainsKey( "password" ) )
        {
          _login = true;
        } // if


        if ( !joiningChat && !TerrariaIRC._ircClient.IsJoined( TerrariaIRC._chatChannel ) )
        {
          new Thread( ConnectToChat ).Start();
          joiningChat = true;
        } // if
        
        if ( TerrariaIRC._dualChannel ) {
          Thread.Sleep( 1000 );  // 5 seconds
          if ( !joiningAction && !TerrariaIRC._ircClient.IsJoined( TerrariaIRC._actionChannel ) )
          {
            new Thread( ConnectToActions ).Start();
            joiningAction = true;
          } // if
        } // if

        Thread.Sleep( 1000 ); // sleepDelay

      } // while

    } // ConnectToIRC ----------------------------------------------------------


    // ConnectToChat +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void ConnectToChat()
    {

      while ( TerrariaIRC._stayConnected )
      {

        try
        {
          Log.Info( string.Format( "Trying to join {0}...", TerrariaIRC._chatChannel ) );
          TerrariaIRC._ircClient.RfcJoin( TerrariaIRC._chatChannel );
          TerrariaIRC._ircClient.ListenOnce();
          Log.Info( string.Format( "Joined {0}.", TerrariaIRC._chatChannel ) );

          if ( _login )
          {
            TerrariaIRC._ircClient.RfcPrivmsg( TerrariaIRC._settings["nickserv"], TerrariaIRC._settings["password"] );
            TerrariaIRC._ircClient.ListenOnce();
          } // if
          TerrariaIRC._ircClient.Listen();

          if ( TerrariaIRC._stayConnected ) 
          { Log.Error( string.Format( "Disconnected from IRC - Attempting to rejoin {0}.", TerrariaIRC._chatChannel ) ); }
        } // try
        catch ( Exception exception )
        {
          Log.Error( string.Format( "Error communicating with IRC server while trying to join {0}.", TerrariaIRC._chatChannel ) );
          Log.Error( exception.Message );
          break;
        } // catch

      } // while

    } // ConnectToChat ---------------------------------------------------------


    // ConnectToActions +++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void ConnectToActions()
    {

      while ( TerrariaIRC._stayConnected )
      {

        try
        {
          Log.Info( string.Format( "Trying to join {0}...", TerrariaIRC._actionChannel ) );
          TerrariaIRC._ircClient.RfcJoin( TerrariaIRC._actionChannel );
          TerrariaIRC._ircClient.ListenOnce();
          Log.Info( string.Format( "Joined {0}.", TerrariaIRC._actionChannel ) );

          if ( _login )
          {
            TerrariaIRC._ircClient.RfcPrivmsg( TerrariaIRC._settings["nickserv"], TerrariaIRC._settings["password"] );
            TerrariaIRC._ircClient.ListenOnce();
          } // if
          TerrariaIRC._ircClient.Listen();

          if ( TerrariaIRC._stayConnected ) 
          { Log.Error( string.Format( "Disconnected from IRC - Attempting to rejoin {0}.", TerrariaIRC._actionChannel ) ); }
        } // try
        catch ( Exception exception )
        {
          Log.Error( string.Format( "Error communicating with IRC server while trying to join {0}.", TerrariaIRC._actionChannel ) );
          Log.Error( exception.Message );
          break;
        } // catch

      } // while

    } // ConnectToActions -----------------------------------------------------


  }
}
