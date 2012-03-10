using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Hooks;
using Meebey.SmartIrc4net;
using TShockAPI;
using Terraria;
using ErrorEventArgs = Meebey.SmartIrc4net.ErrorEventArgs;

namespace TerrariaIRC
{
    [APIVersion(1, 11)]
    public class TerrariaIRC : TerrariaPlugin
    {
        #region Plugin Properties
        public override string Name
        {
            get { return "TerrariaIRC"; }
        }

        public override string Author
        {
            get { return "Deathmax"; }
        }

        public override string Description
        {
            get { return "Provides an interface between IRC and Terraria"; }
        }

        public override Version Version
        {
            get { return new Version(1, 2, 0, 6); }
        }
        public TerrariaIRC(Main game) : base(game)
        {
            Order = 10;
        }
        #endregion

        #region Plugin Vars
        public static IrcClient irc = new IrcClient();
        private static Settings settings = new Settings();
        public static string settingsfiles = Path.Combine(TShock.SavePath, "irc", "settings.txt");
        public static bool StayConnected = true;
        #endregion

        #region Plugin overrides
        public override void Initialize()
        {
            ServerHooks.Chat  += OnChat;
            ServerHooks.Join  += OnJoin;
            ServerHooks.Leave += OnLeave;
            irc.Encoding = System.Text.Encoding.ASCII;
            irc.SendDelay = 300;
            irc.ActiveChannelSyncing = true;
            irc.AutoRejoinOnKick = true;
            //irc.OnQueryMessage += OnQueryMessage;
            irc.OnError += OnError;
            irc.OnChannelMessage += OnChannelMessage;
            irc.OnRawMessage += OnRawMessage;
            if (!settings.Load())
            {
              Log.Error( "Settings failed to load, aborting IRC connection." );
              return;
            }
            Commands.Init();
            new Thread( Connect ).Start();
        }

        protected override void Dispose(bool disposing)
        {
          if ( disposing )
          {

            StayConnected = false;
            if ( irc.IsConnected )
              irc.Disconnect();
            ServerHooks.Chat  -= OnChat;
            ServerHooks.Join  -= OnJoin;
            ServerHooks.Leave -= OnLeave;
            base.Dispose( disposing );

          } // if
        }
        #endregion

        #region IRC methods
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
        public static void OnError(object sender, ErrorEventArgs e)
        {
          Log.Error( "IRC Error: " + e.Data.RawMessage );
        }

        void OnChannelMessage(object sender, IrcEventArgs e)
        {
            var message = e.Data.Message;
            if ( message.StartsWith( "!" ) )
            {
              if ( message.ToLower() == "!players" )
              {
                var reply = TShock.Players.Where( player => player != null ).Where( player => player.RealPlayer ).Aggregate( "", ( current, player ) => current + (current == "" ? player.Name : ", " + player.Name) );
                irc.SendMessage( SendType.Message, settings["channel"], "Current Players: " + reply );
              } // if
              else
              {
                if ( !message.ToLower().Contains( "superadmin" ) )
                {
                  if ( IsAllowed( e.Data.Nick ) )
                  {
                    var user = new IRCPlayer( e.Data.Nick ) { Group = new SuperAdminGroup() };
                    String conCommand = "/" + message.TrimStart( '!' );
                    Log.Info( user + " invoked command: " + conCommand );
                    TShockAPI.Commands.HandleCommand( user, conCommand );
                    foreach ( var outputMessage in user.Output )
                    {
                      irc.RfcPrivmsg( e.Data.Nick, outputMessage );
                    } // for
                  } // if
                  else
                  {
                    Log.Warn( e.Data.Nick + " attempted to invoked command: " + message );
                    irc.RfcPrivmsg( e.Data.Nick, "You are not authorized to perform commands on the server." );
                  } // else
                } // if
                else
                {
                  Log.Warn( e.Data.Nick + " attempted to invoked command: " + message );
                  irc.RfcPrivmsg( e.Data.Nick, "Command not allowed through irc." );
                } // else
              } // else
            } // if
            else
            {
              TShock.Utils.Broadcast( string.Format( "(IRC)<{0}> {1}", e.Data.Nick,
                  TShock.Utils.SanitizeString( Regex.Replace( message, (char) 3 + "[0-9]{1,2}(,[0-9]{1,2})?", String.Empty ) ) ), Color.Green );
            } // else
        }

        void OnRawMessage(object sender, IrcEventArgs e)
        {
          Debug.Write( e.Data.RawMessage );
        }

        public static void Connect()
        {
          int maxAttempts  = 3;
          int attemptCount = 0;
          int sleepDelay   = 15 * 60 * 1000; // 15 minutes
            while (StayConnected & attemptCount < maxAttempts )
            {
              Log.Info( "Connecting to " + settings["server"] + ":" + settings["port"] + "..." );
                try
                {
                    irc.Connect(settings["server"], int.Parse(settings["port"]));
                    irc.ListenOnce();
                    Log.Info( "Connected to IRC server." );
                } // try
                catch (Exception e)
                {
                  Log.Error( "Error connecting to IRC server." );
                  Log.Error( e.Message );
                  Thread.Sleep( sleepDelay ); 
                  //return;
                } // catch
                irc.Login(settings["botname"], "TerrariaIRC");
                irc.ListenOnce();
                Log.Info( "Trying to join " + settings["channel"] + "..." );
                irc.RfcJoin(settings["channel"]);
                irc.ListenOnce();
                Log.Info( "Joined " + settings["channel"] );
                if (settings.ContainsKey("nickserv") && settings.ContainsKey("password"))
                {
                    irc.RfcPrivmsg(settings["nickserv"], settings["password"]);
                    irc.ListenOnce();
                } // if
                irc.Listen();
                Log.Error( "Disconnected from IRS... Attempting to reconnect: " + attemptCount );
                attemptCount++;
            }
        }

        public static bool IsAllowed(string nick)
        {
            //Theres an issue with IsOp, if the ircd has anything higher like +a, IsOp will return false;
            if (bool.Parse(settings["allowop"]))
            {
                return (from user in (from DictionaryEntry channeluser in irc.GetChannel(settings["channel"]).Users select (ChannelUser) channeluser.Value) where user.Nick == nick select user.IsOp).FirstOrDefault();
            }
            return false;
        }

        public static bool CompareIrcUser(IrcUser user1, IrcUser user2)
        {
            return (user1.Host == user2.Host && user1.Ident == user2.Ident && user1.Realname == user2.Realname);
        }
        #endregion

        #region Plugin hooks
        void OnChat(messageBuffer msg, int player, string text, System.ComponentModel.HandledEventArgs e)
        {
            if (!irc.IsConnected) return;
            var tsplr = TShock.Players[msg.whoAmI];
            if (tsplr == null)
                return;
            if (!TShock.Utils.ValidString(text))
                return;
            if (text.StartsWith("/"))
                return;
            if (tsplr.mute)
                return;
            irc.SendMessage(SendType.Message, settings["channel"], string.Format("({0}){1}: {2}", 
                tsplr.Group.Name, tsplr.Name, text));
        }

        void OnJoin(int player, System.ComponentModel.HandledEventArgs e)
        {
            if (!irc.IsConnected) return;
            if (e.Handled) return;
            irc.SendMessage(SendType.Message, settings["channel"], string.Format("{0} joined the server.", 
                Main.player[player].name));
        }

        void OnLeave(int player)
        {
            if (!irc.IsConnected) return;
            irc.SendMessage(SendType.Message, settings["channel"], string.Format("{0} left the server.",
                Main.player[player].name));
        }
        #endregion
    }
}