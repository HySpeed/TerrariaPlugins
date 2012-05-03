using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Streams;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Hooks;
using Meebey.SmartIrc4net;
using TShockAPI;
using Terraria;
using ErrorEventArgs = Meebey.SmartIrc4net.ErrorEventArgs;

// TerrariaIRC *****************************************************************
namespace TerrariaIRC
{

  // TerrariaIRC ***************************************************************
  [APIVersion( 1, 11 )]
  public class TerrariaIRC : TerrariaPlugin
  {
    #region Plugin Vars
    public  static IrcClient irc           = new IrcClient();
    public  static string    settingsFile  = Path.Combine( TShock.SavePath, "irc", "settings.txt"  );
    public  static string    settingsFile2 = Path.Combine( TShock.SavePath, "irc", "settings2.txt" );
    private static Settings  settings      = new Settings();
    private static Settings  settings2     = new Settings();
    private static int       maxAttempts   = 3;
    private static int       _attemptCount  = 0;
    private static int       sleepDelay    = 60000; // 1 minute
    private static volatile  bool _stayConnected = true;
    private static bool      _loggedIn = false;
    #endregion -----------------------------------------------------------------


    #region Plugin overrides
    // Initialize ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public override void Initialize()
    {

      ServerHooks.Chat  += OnChat;
      ServerHooks.Join  += OnJoin;
      ServerHooks.Leave += OnLeave;
			NetHooks.GetData  += ParseData;

      SetupIRC();
      if ( !settings.Load( settingsFile ) )
      {
        Log.Error( "Settings failed to load, aborting IRC connection." );
        return;
      } // if
      settings2.Load( settingsFile2 );
      Commands.Init();

      new Thread( ConnectToIRC ).Start();

    } // Initialize ------------------------------------------------------------


    // Dispose +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    protected override void Dispose( bool disposing )
    {

      if ( disposing )
      {
        ServerHooks.Chat  -= OnChat;
        ServerHooks.Join  -= OnJoin;
        ServerHooks.Leave -= OnLeave;
        NetHooks.GetData  -= ParseData;

        _stayConnected = false;
        if ( irc.IsConnected ) 
        { 
          _stayConnected = false;
          irc.Disconnect(); 
        } // if

        base.Dispose( disposing );
      } // if

    } // Dispose ---------------------------------------------------------------


    // SetupIRC ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    private void SetupIRC() 
    {
      irc.Encoding             = System.Text.Encoding.ASCII;
      irc.SendDelay            = 300;
      irc.ActiveChannelSyncing = true;
      irc.AutoRejoinOnKick     = true;
      irc.OnError             += OnError;
      irc.OnChannelMessage    += OnChannelMessage;
      irc.OnRawMessage        += OnRawMessage;
    } // SetupIRC --------------------------------------------------------------
    #endregion


    #region IRC methods
    // OnChannelMessage ++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnChannelMessage( object sender, IrcEventArgs ircEvent )
    {
      var message = ircEvent.Data.Message;
      if ( message.StartsWith( "!" ) )
      {
        if ( message.ToLower() == "!players" )
        {
          ActionPlayers( sender, ircEvent );
        } // if
        else
        {
          ActionCommand( sender, ircEvent );
        } // else
      } // if
      else
      {
        TShock.Utils.Broadcast( string.Format( "(IRC)<{0}> {1}", ircEvent.Data.Nick,
            TShock.Utils.SanitizeString( Regex.Replace( message, (char) 3 + "[0-9]{1,2}(,[0-9]{1,2})?", String.Empty ) ) ), Color.Green );
      } // else
    } // OnChannelMessage ------------------------------------------------------


    // OnRawMessage ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnRawMessage( object sender, IrcEventArgs e )
    {
      Debug.Write( e.Data.RawMessage );
    } // OnRawMessage ----------------------------------------------------------


    // ConnectToIRC ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void ConnectToIRC()
    {
      bool joiningChat   = false;
      bool joiningAction = false;

      while ( _stayConnected && (_attemptCount < maxAttempts) )
      {

        try
        {
          if ( !irc.IsConnected ) 
          {
            Log.Info( "Connecting to " + settings["server"] + ":" + settings["port"] + "..." );
            irc.Connect( settings["server"], int.Parse( settings["port"] ) );
            irc.ListenOnce();
            Log.Info( "Connected to IRC server." );
          } // if
        } // try
        catch ( Exception exception )
        {
          Log.Error( "Error connecting to IRC server " + settings["server"] + " on port " + settings["port"] + " (" + _attemptCount + ")" );
          Log.Error( exception.Message );
          if ( _stayConnected ) { Thread.Sleep( sleepDelay ); }
          _attemptCount++;
        } // catch

        try
        {
          if ( !_loggedIn ) 
          {
            Log.Info( "Trying to login as " + settings["botname"] + "..." );
            irc.Login( settings["botname"], "TerrariaIRC" );
            irc.ListenOnce();
            _loggedIn = true;
            Log.Info( "Logged in as " + settings["botname"] );
          } // if
        } // try
        catch ( Exception exception )
        {
          Log.Error( "Error logging on as " + settings["botname"] + " (" + _attemptCount + ")" );
          Log.Error( exception.Message );
          _attemptCount++;
        } // catch

        if ( !joiningChat && !irc.IsJoined( settings["channel"] ) )
        {
          new Thread( ConnectToChat ).Start();
          joiningChat = true;
        } // if

        Thread.Sleep( 1000 );  // 5 seconds
        
        if ( !joiningAction && !irc.IsJoined( settings2["channel"] ) )
        {
          new Thread( ConnectToActions ).Start();
          joiningAction = true;
        } // if

        Thread.Sleep( 1000 ); // sleepDelay

      } // while

    } // ConnectToIRC ----------------------------------------------------------


    // ConnectToChat +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void ConnectToChat()
    {

      while ( _stayConnected )
      {
        try
        {
          Log.Info( "Trying to join " + settings["channel"] + "..." );
          irc.RfcJoin( settings["channel"] );
          irc.ListenOnce();
          Log.Info( "Joined " + settings["channel"] );
          if ( settings.ContainsKey( "nickserv" ) && settings.ContainsKey( "password" ) )
          {
            irc.RfcPrivmsg( settings["nickserv"], settings["password"] );
            irc.ListenOnce();
          } // if
          irc.Listen();
          if ( _stayConnected ) 
            Log.Error( "Disconnected from IRC... Attempting to rejoin " + settings["channel"] );
        } // try
        catch ( Exception exception )
        {
          Log.Error( "Error communicating with IRC server." );
          Log.Error( exception.Message );
          return;
        } // catch

      } // while

    } // ConnectToChat ---------------------------------------------------------


    // ConnectToActions +++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void ConnectToActions()
    {

      while ( _stayConnected )
      {
        try
        {
          Log.Info( "Trying to join " + settings2["channel"] + "..." );
          irc.RfcJoin( settings2["channel"] );
          irc.ListenOnce();
          Log.Info( "Joined " + settings2["channel"] );
          if ( settings2.ContainsKey( "nickserv" ) && settings2.ContainsKey( "password" ) )
          {
            irc.RfcPrivmsg( settings2["nickserv"], settings2["password"] );
            irc.ListenOnce();
          } // if
          irc.Listen();
          if ( _stayConnected )
            Log.Error( "Disconnected from IRC... Attempting to rejoin " + settings2["channel"] );
        } // try
        catch ( Exception exception )
        {
          Log.Error( "Error communicating with IRC server." );
          Log.Error( exception.Message );
          return;
        } // catch
      } // while

    } // ConnectToActions -----------------------------------------------------


    // sendIRCMessage ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void sendIRCMessage( string message )
    {
      irc.SendMessage( SendType.Message, settings["channel"], message );
    } // sendIRCMessage --------------------------------------------------------
    #endregion // IRCRegion ----------------------------------------------------


    /***************************************************************************
     * Plugin Hooks                                                            *
     **************************************************************************/ 
    #region Plugin hooks
    // OnChat ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnChat( messageBuffer   message, 
                 int              playerId, 
                 string           text, 
                 HandledEventArgs eventArgs )
    {
      if ( !irc.IsConnected ) return;
      var player = TShock.Players[message.whoAmI];
      if ( player == null ) return;
      if ( !TShock.Utils.ValidString( text ) ) return;
      if ( player.mute ) return;

      //if ( text.StartsWith( "/" ) ) return;
      if ( text.StartsWith( "/" ) ) 
      {
        text = ScrubCommand( text );
        irc.SendMessage( SendType.Message, settings2["channel"], string.Format( "{0} ({1}): command: {2}",
                                           player.Name, player.Group.Name, text ) );
      } // if
      else 
      {
        irc.SendMessage( SendType.Message, settings["channel"], string.Format( "{0} ({1}): comment: {2}",
                                           player.Name, player.Group.Name, text ) );
      } // else

    } // OnChat ----------------------------------------------------------------


    // OnJoin ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnJoin( int player, HandledEventArgs e )
    {
      if ( !irc.IsConnected ) return;
      if ( e.Handled ) return;

      irc.SendMessage( SendType.Message, settings["channel"], 
                       string.Format( "Joined[{0}]: {1} ({2}/{3}) - {4}({5})", 
                                      CountPlayers() + 1,
                                      Main.player[player].name,
                                      Main.player[player].statLifeMax,
                                      Main.player[player].statManaMax,
                                      Main.player[player].inventory[0].name,
                                      Main.player[player].inventory[0].stack ) );

    } // OnJoin ----------------------------------------------------------------


    // OnLeave +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnLeave( int player )
    {
      if ( !irc.IsConnected ) return;

      irc.SendMessage( SendType.Message, settings["channel"],
                        string.Format( "Left[{0}]: {1}",
                        CountPlayers(),
                        Main.player[player].name ) );

    } // OnLeave ---------------------------------------------------------------


    private short prevItemType = -99;
    // ParseData +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
		private void ParseData( GetDataEventArgs args )
		{

			try
			{
				PacketTypes packet = args.MsgID;
				using ( var data = new MemoryStream( args.Msg.readBuffer, args.Index, args.Length ) )
				{
					TSPlayer player = TShock.Players[args.Msg.whoAmI];
					switch ( packet )
					{
						case PacketTypes.ChestItem:
            {
              string action, itemName;
              short  chestID   = data.ReadInt16();
              byte   itemSlot  = data.ReadInt8();
              byte   itemStack = data.ReadInt8();
              byte   prefix    = data.ReadInt8();
              short  itemType  = data.ReadInt16();
              var    oldItem   = Main.chest[chestID].item[itemSlot];
              if ( oldItem.name != null && oldItem.name.Length > 0 ) 
              { 
                action = "Get"; 
                itemName = oldItem.name;
              } // if
              else 
              {
                var newItem = new Item();
                newItem.netDefaults( itemType );
                newItem.Prefix( prefix );
                newItem.AffixName();
                action = "Put";
                itemName = newItem.name;
              } // else

              if ( itemType != prevItemType ) 
              {
              irc.SendMessage( SendType.Message, settings2["channel"], 
                               string.Format( "{0} ({1}): C{2}: {3}",
                               player.Name, player.Group.Name, action, itemName ) );
                prevItemType = itemType;
              } // if
							break;
						} // case
					} // switch
				} // using
			} // try
			catch ( Exception e )
			{
				Console.WriteLine( e.Message + "(" + e.StackTrace + ")" );
			} // catch

    } // ParseData -------------------------------------------------------------
	
            
    // OnError +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void OnError( object sender, ErrorEventArgs e )
    {
      Log.Error( "IRC Error: " + e.Data.RawMessage );
    } // OnError ---------------------------------------------------------------
    #endregion


    /***************************************************************************
     * Data Handlers                                                           *
     **************************************************************************/ 
    // CountPlayers ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    private int CountPlayers()
    {
      int result = 0;
      foreach ( TSPlayer player in TShock.Players ) 
      {
        if ( player != null && player.Active ) { result++; }
      } // foreach

      return result;
    } // CountPlayers ----------------------------------------------------------


    // resetConnectionSettings +++++++++++++++++++++++++++++++++++++++++++++++++
    public static void resetConnectionSettings()
    {
      _attemptCount  = 0;
      _stayConnected = true;
      _loggedIn      = false;
    } // resetConnectionSettings -----------------------------------------------


    // ScrubCommand ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    // filter out command arguments with password info
    private string ScrubCommand( string text ) {
      string result = text.Remove( 0, 1 ).ToLower().Trim();

      if ( result.StartsWith( "register" ) ) result = "register";
      if ( result.StartsWith( "login"    ) ) result = "login";
      if ( result.StartsWith( "password" ) ) result = "password";
      if ( result.StartsWith( "cunlock"  ) ) result = "cunlock";

      return result;
    } // ScrubCommand ----------------------------------------------------------

    
    // ActionCommand +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    private void ActionCommand( object       sender, 
                                IrcEventArgs ircEvent)
    {
      var message = ircEvent.Data.Message;
      if ( !message.ToLower().Contains( "superadmin" ) )
      {
        if ( IsAllowed( ircEvent.Data.Nick ) )
        {
          var user = new IRCPlayer( ircEvent.Data.Nick ) { Group = new SuperAdminGroup() };
          String conCommand = "/" + message.TrimStart( '!' );
          Log.Info( user + " invoked command: " + conCommand );
          TShockAPI.Commands.HandleCommand( user, conCommand );
          foreach ( var outputMessage in user.Output )
          {
            irc.RfcPrivmsg( ircEvent.Data.Nick, outputMessage );
          } // for
        } // if
        else
        {
          Log.Warn( ircEvent.Data.Nick + " attempted to invoked command: " + message );
          irc.RfcPrivmsg( ircEvent.Data.Nick, "You are not authorized to perform commands on the server." );
        } // else
      } // if
      else
      {
        Log.Warn( ircEvent.Data.Nick + " attempted to invoked command: " + message );
        irc.RfcPrivmsg( ircEvent.Data.Nick, "Command not allowed through irc." );
      } // else
    } // ActionCommand ---------------------------------------------------------


    // ActionPlayers +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    private void ActionPlayers( object       sender, 
                                IrcEventArgs ircEvent )
    {
      var reply = TShock.Players.Where( player => player != null )
                                .Where( player => player.RealPlayer )
                                .Aggregate( "", ( current, player ) => current + (current == "" ? player.Name : ", " + player.Name) );
      irc.SendMessage( SendType.Message, settings["channel"], "Current Players: " + reply );
    } // ActionPlayers ---------------------------------------------------------


    // IsAllowed +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static bool IsAllowed( string nick )
    {
      // DMax: Theres an issue with IsOp, if the ircd has anything higher like +a, IsOp will return false;
      if ( bool.Parse( settings["allowop"] ) )
      {
        return (from user in (from DictionaryEntry channeluser in irc.GetChannel( settings["channel"] ).Users select (ChannelUser) channeluser.Value) where user.Nick == nick select user.IsOp).FirstOrDefault();
      } // if
      return false;
    } // IsAllowed -------------------------------------------------------------


    // CompareIrcUser ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static bool CompareIrcUser( IrcUser user1, IrcUser user2 )
    {
      return (user1.Host == user2.Host && user1.Ident == user2.Ident && user1.Realname == user2.Realname);
    } // CompareIrcUser --------------------------------------------------------

    
    /***************************************************************************
     * Plugin Properties                                                       *
     **************************************************************************/ 
    #region Plugin Properties
    // Name ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public override string Name
    {
      get { return "TerrariaIRC"; }
    } // Name ------------------------------------------------------------------


    // Author ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public override string Author
    {
      get { return "Deathmax, _Jon"; }
    } // Author ----------------------------------------------------------------


    // Description +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public override string Description
    {
      get { return "Provides an interface between IRC and Terraria.\n" +
                   "Also provides player from commands in IRC channel (/iInfo)"; }
    } // Description -----------------------------------------------------------


    // Version +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
   public override Version Version
    {
      get { return new Version( 2, 0, 0, 0 ); }
    } // Versin ----------------------------------------------------------------

    
    // TerrariaIRC +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public TerrariaIRC( Main game ) : base( game )
    {
      Order = -9;
    } // TerrariaIRC -----------------------------------------------------------
    #endregion

  } // TerrariaIRC -------------------------------------------------------------



    // _Jon : not functional 03-03-12 -> moved to "OnChannelMessage"
    /*      
            public static void OnQueryMessage(object sender, IrcEventArgs e)
            {
              var message = e.Data.Message;
              if ( IsAllowed( e.Data.Nick ) )
              {
                if ( message.StartsWith( "!" ) )
                {
                  //Console.WriteLine( "~ ! : " + message );
                  var user = new IRCPlayer( e.Data.Nick ) { Group = new SuperAdminGroup() };
                  String conCommand = "/" + message.TrimStart( '!' );
                  Log.Info( user + " invoked command: " + conCommand );
                  TShockAPI.Commands.HandleCommand( user, conCommand );
                  foreach ( var t in user.Output )
                  {
                    irc.RfcPrivmsg( e.Data.Nick, t );
                  } // for
                }
              }
              else
              {
                Log.Warn( e.Data.Nick + " attempted to invoked command: " + message );
                irc.RfcPrivmsg( e.Data.Nick, "You are not allowed to perform that action." );
              }
            }
    */
} // TerrariaIRC ===============================================================
