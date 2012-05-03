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
    public  static IrcClient _ircClient    = new IrcClient();
    public  static string    settingsFile  = Path.Combine( TShock.SavePath, "irc", "settings.txt"  );
    
    public static Settings  _settings      = new Settings();
    public static int       _maxAttempts   = 3;
    public static int       _attemptCount  = 0;
    public static int       _sleepDelay    = 60000; // 1 minute
    public static volatile  bool _stayConnected = true;
    public static bool      _loggedIn = false;
    public static bool      _dualChannel  = false;
    public static string    _chatChannel, _actionChannel;
    private static short    prevItemType  = -99;

    #endregion -----------------------------------------------------------------


    #region Plugin overrides
    // Initialize ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public override void Initialize()
    {

      ServerHooks.Chat  += OnChat;
      ServerHooks.Join  += OnJoin;
      ServerHooks.Leave += OnLeave;
			//NetHooks.GetData  += ParseData;

      SetupIRC();
      if ( !_settings.Load( settingsFile ) )
      {
        Log.Error( "Settings failed to load, aborting IRC connection." );
        return;
      } // if
      
      Commands.Init();
      _dualChannel = bool.Parse( _settings["dualchannels"] );
      Console.WriteLine( "~ dc: " + _dualChannel );
      _chatChannel = _settings["chatchannel"];
      if ( _dualChannel ) 
      { _actionChannel = _settings["actionchannel"]; } // if
      else
      { _actionChannel = _settings["chatchannel"];   } // if

      new Thread( Connect.ConnectToIRC ).Start();

    } // Initialize ------------------------------------------------------------


    // Dispose +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    protected override void Dispose( bool disposing )
    {

      if ( disposing )
      {
        ServerHooks.Chat  -= OnChat;
        ServerHooks.Join  -= OnJoin;
        ServerHooks.Leave -= OnLeave;
        //NetHooks.GetData  -= ParseData;

        _stayConnected = false;
        if ( _ircClient.IsConnected ) 
        { 
          _stayConnected = false;
          _ircClient.Disconnect(); 
        } // if

        base.Dispose( disposing );
      } // if

    } // Dispose ---------------------------------------------------------------


    // SetupIRC ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    private void SetupIRC() 
    {
      _ircClient.Encoding             = System.Text.Encoding.ASCII;
      _ircClient.SendDelay            = 300;
      _ircClient.ActiveChannelSyncing = true;
      _ircClient.AutoRejoinOnKick     = true;
      _ircClient.OnError             += OnError;
      _ircClient.OnChannelMessage    += OnChannelMessage;
      _ircClient.OnRawMessage        += OnRawMessage;
    } // SetupIRC --------------------------------------------------------------
    #endregion


    #region IRC methods
    // OnChannelMessage ++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnChannelMessage( object sender, IrcEventArgs ircEvent )
    {
      var message = ircEvent.Data.Message;

      if ( message.StartsWith( "!" ) )
      {
        if ( message.ToLower().Equals( "!players" ) )
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
                                TShock.Utils.SanitizeString( Regex.Replace( message, 
                                (char) 3 + "[0-9]{1,2}(,[0-9]{1,2})?", String.Empty ) ) ), 
                                Color.Green );
      } // else

    } // OnChannelMessage ------------------------------------------------------


    // OnRawMessage ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnRawMessage( object sender, IrcEventArgs e )
    {
      Debug.Write( e.Data.RawMessage );
    } // OnRawMessage ----------------------------------------------------------


    // sendIRCMessage ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static void sendIRCMessage( string message )
    {
      _ircClient.SendMessage( SendType.Message, _chatChannel, message );
    } // sendIRCMessage --------------------------------------------------------
    #endregion // IRCRegion ----------------------------------------------------


    /***************************************************************************
     * Plugin Hooks                                                            *
     **************************************************************************/ 
    #region Plugin hooks
    // OnChat ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnChat( messageBuffer    message, 
                 int              playerId, 
                 string           text, 
                 HandledEventArgs eventArgs )
    {
      var player = TShock.Players[message.whoAmI];

      if ( !_ircClient.IsConnected ) return;
      if ( player == null ) return;
      if ( !TShock.Utils.ValidString( text ) ) return;
      if ( player.mute ) return;

      if ( text.StartsWith( "/" ) ) 
      {
        text = ScrubCommand( text );
        _ircClient.SendMessage( SendType.Message, _actionChannel, string.Format( "{0} ({1}): cmd: {2}",
                         player.Name, player.Group.Name, text ) );
      } // if
      else 
      {
        _ircClient.SendMessage( SendType.Message, _chatChannel, string.Format( "{0} ({1}): chat: {2}",
                         player.Name, player.Group.Name, text ) );
      } // else

    } // OnChat ----------------------------------------------------------------


    // OnJoin ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnJoin( int              playerId,
                 HandledEventArgs eventArgs )
    {
      string firstItem = "no slot 0 item (what an idiot...)";

      if ( !_ircClient.IsConnected ) return;
      if ( eventArgs.Handled ) return;

      var tePlayer = Main.player[playerId];
      var tsPlayer = TShock.Players[playerId];
      if ( tePlayer.inventory[0].name != "" ) { firstItem = tePlayer.inventory[0].name; }


      _ircClient.SendMessage( SendType.Message, _chatChannel, 
                              string.Format( "Join[{0}]: {1} ({2}) ({3}/{4}) - {5} ({6})", 
                                             CountPlayers() + 1,
                                             tePlayer.name,
                                             tsPlayer.Country,
                                             tePlayer.statLifeMax,
                                             tePlayer.statManaMax,
                                             firstItem,
                                             tePlayer.inventory[0].stack ) );

    } // OnJoin ----------------------------------------------------------------


    // OnLeave +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    void OnLeave( int player )
    {
      if ( !_ircClient.IsConnected ) return;

      _ircClient.SendMessage( SendType.Message, _chatChannel,
                        string.Format( "Left[{0}]: {1}",
                        CountPlayers(),
                        Main.player[player].name ) );

    } // OnLeave ---------------------------------------------------------------


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
                action = "cGet"; 
                itemName = oldItem.name;
              } // if
              else 
              {
                var newItem = new Item();
                newItem.netDefaults( itemType );
                newItem.Prefix( prefix );
                newItem.AffixName();
                action = "cPut";
                itemName = newItem.name;
              } // else

              if ( itemType != prevItemType ) 
              {
                _ircClient.SendMessage( SendType.Message, _actionChannel, 
                                 string.Format( "{0} ({1}): {2}: {3}",
                                 player.Name, player.Group.Name, action, itemName ) );
                prevItemType = itemType;
              } // if
							break;
						} // case
					} // switch
				} // using
			} // try
			catch ( Exception exception )
			{
				Console.WriteLine( exception.Message + "(" + exception.StackTrace + ")" );
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
          Log.Info( string.Format( "{0} invoked command: {1}",  user, conCommand ) );
          TShockAPI.Commands.HandleCommand( user, conCommand );

          foreach ( var outputMessage in user.Output )
          {
            _ircClient.RfcPrivmsg( ircEvent.Data.Nick, outputMessage );
          } // for

        } // if
        else
        {
          Log.Warn( string.Format( "{0} attempted to invoked command: {1}", ircEvent.Data.Nick, message ) );
          _ircClient.RfcPrivmsg( ircEvent.Data.Nick, "You are not authorized to perform commands on the server." );
        } // else
      } // if
      else
      {
        Log.Warn( string.Format( "{0} attempted to invoked command: {1}", ircEvent.Data.Nick, message ) );
        _ircClient.RfcPrivmsg( ircEvent.Data.Nick, "This command is not allowed through irc." );
      } // else

    } // ActionCommand ---------------------------------------------------------


    // ActionPlayers +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    private void ActionPlayers( object       sender, 
                                IrcEventArgs ircEvent )
    {
      var reply = TShock.Players.Where( player => player != null )
                                .Where( player => player.RealPlayer )
                                .Aggregate( "", ( current, player ) => current + (current == "" ? player.Name : ", " + player.Name) );
      _ircClient.SendMessage( SendType.Message, _chatChannel, "Current Players: " + reply );
    } // ActionPlayers ---------------------------------------------------------


    // IsAllowed +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public static bool IsAllowed( string nick )
    {
      // DMax: Theres an issue with IsOp, if the ircd has anything higher like +a, IsOp will return false;
      if ( bool.Parse( _settings["allowop"] ) )
      {
        return (from user in (
                   from DictionaryEntry channeluser in _ircClient.GetChannel( _chatChannel ).Users 
                                                         select (ChannelUser) channeluser.Value ) 
                                                         where user.Nick == nick 
                                                           select user.IsOp ).FirstOrDefault();
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
      get { return new Version( 2, 0, 1, 0 ); }
    } // Versin ----------------------------------------------------------------

    
    // TerrariaIRC +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
    public TerrariaIRC( Main game ) : base( game )
    {
      Order = 9;
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
