using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;

// TerrariaIRC *****************************************************************
namespace TerrariaIRC
{

  
    // Commands ****************************************************************
    class Commands
    {

      // Init ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      public static void Init()
      {
        TShockAPI.Commands.ChatCommands.Add( new Command( "manageirc", Reconnect,    "reconirc" ) );
        TShockAPI.Commands.ChatCommands.Add( new Command( "manageirc", IrcInventory, "iinv"     ) );
        TShockAPI.Commands.ChatCommands.Add( new Command( "manageirc", IrcInfo,      "iinfo"    ) );
      } // Init ----------------------------------------------------------------


      // Reconnect +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      // Because the 'Connect' method is set to auto-reconnect, disconnecting
      // . will restart that loop.
      public static void Reconnect( CommandArgs args )
      {
        TerrariaIRC.resetConnectionSettings();
        TerrariaIRC.irc.Disconnect();
        TShock.Utils.SendLogs( "Disconnected from IRC, reconnecting.", Color.Red );
      } // Reconnect -----------------------------------------------------------


      // IrcInventory ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      public static void IrcInventory( CommandArgs args )
      {
        if ( args.Parameters.Count <= 1 ) 
        {
          TerrariaIRC.sendIRCMessage( "Invalid syntax. Proper Syntax: /iinv <player> [ INV <row> | ACC | AMM | ARM ]" );
        } // if
        else if ( args.Parameters.Count > 1 )
        {
          TShockAPI.TSPlayer player;
          string playerName, action;
          playerName = args.Parameters[0];

          player = findPlayer( playerName ); 
          if ( player != null ) 
          {
            action = args.Parameters[1].ToUpper();

                 if ( action.Equals( "ACC" ) ) { showAcc( player ); } // if
            else if ( action.Equals( "ARM" ) ) { showArm( player ); } // if
            else if ( action.Equals( "AMM" ) ) { showAmm( player ); } // if
            else if ( action.Equals( "INV" ) ) 
            { 
              if ( args.Parameters.Count == 3 ) 
              {
                showInv( player, Convert.ToInt32( args.Parameters[2] ) );
              } // if
              else 
              {
                TerrariaIRC.sendIRCMessage( "Row required for INV action (e.g. iirc name row)" );
              } // else
            } // if
            else 
            {
              TerrariaIRC.sendIRCMessage( string.Format( "Invalid action: {0}", action ) );
              TerrariaIRC.sendIRCMessage( "Invalid syntax. Proper Syntax: /iinv <player> [ INV <row> | ACC | AMM | ARM ]" );
            } // else

          } // if

        } // else if

      } // IrcInventory --------------------------------------------------------
        // this line gets 'active' items, that is, item slots that have an item in them.
        //var activeItems = player.TPlayer.inventory.Where( p => p.active ).ToList();  


      // showAcc +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      private static void showAcc( TShockAPI.TSPlayer player )
      {
        StringBuilder response = new StringBuilder();
        String itemName;
        int firstSlot = 3, lastSlot = 8;

        for ( int index = firstSlot; index <= lastSlot; index++ ) {
          itemName = player.TPlayer.armor[index].name;
          if ( itemName.Length == 0 ) { itemName = "(no item)"; } // if
          response.Append( itemName );
          if ( index < lastSlot ) { response.Append( " | " ); } // if
        } // for

        TerrariaIRC.sendIRCMessage( string.Format( "{0} Access: {1}", player.Name, response ) );
      } // showAcc -------------------------------------------------------------


      // showArm +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      private static void showArm( TShockAPI.TSPlayer player )
      {
        StringBuilder response = new StringBuilder();
        String itemName;
        int firstSlot = 0, lastSlot = 2;

        for ( int index = firstSlot; index <= lastSlot; index++ ) {
          itemName = player.TPlayer.armor[index].name;
          if ( itemName.Length == 0 ) { itemName = "(no item)"; } // if
          response.Append( itemName );
          if ( index < lastSlot ) { response.Append( " | " ); } // if
        } // for

        TerrariaIRC.sendIRCMessage( string.Format( "{0} Armour: {1}", player.Name, response ) );
      } // showArm -------------------------------------------------------------


      // showAmm +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      private static void showAmm( TShockAPI.TSPlayer player )
      {
        StringBuilder response = new StringBuilder();
        String itemName;
        int firstSlot = 44, lastSlot = 47;

        for ( int index = firstSlot; index <= lastSlot; index++ ) {
          itemName = player.TPlayer.inventory[index].name;
          if ( itemName.Length == 0 ) { itemName = "(no item)"; } // if
          response.Append( itemName ).Append( " (" );
          response.Append( player.TPlayer.inventory[index].stack ).Append( ")" );
          if ( index < lastSlot ) { response.Append( " | " ); } // if
        } // for

        TerrariaIRC.sendIRCMessage( string.Format( "{0} Ammo: {1}", player.Name, response ) );
      } // showAmm -------------------------------------------------------------


      // showInv +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      private static void showInv( TShockAPI.TSPlayer player, int row )
      {
        StringBuilder response = new StringBuilder();
        String itemName;
        List<int> firstSlot = new List<int>(5) {  0, 0, 10, 20, 30 };
        List<int> lastSlot  = new List<int>(5) { -1, 9, 19, 29, 39 };

        if ( row < firstSlot.Count && lastSlot[row] > 0 ) 
        {

          for ( int index = firstSlot[row]; index <= lastSlot[row]; index++ ) {
            itemName = player.TPlayer.inventory[index].name;
            if ( itemName.Length == 0 ) { itemName = "(no item)"; } // if
            response.Append( itemName ).Append( " (" );
            response.Append( player.TPlayer.inventory[index].stack ).Append( ")" );
            if ( index < lastSlot[row] ) { response.Append( " | " ); } // if
          } // for

          TerrariaIRC.sendIRCMessage( string.Format( "{0} Inv Row [{1}]: {2}", player.Name, row, response ) );
        } // if
        else 
        {
          TerrariaIRC.sendIRCMessage( string.Format( "Invalid row: {0}.  Only 1 - 4 are allowed.", row ) );
        } // else

      } // showInv -------------------------------------------------------------


      // IrcInfo +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      public static void IrcInfo( CommandArgs args )
      {
        if ( args.Parameters.Count <= 1 ) 
        {
          TerrariaIRC.sendIRCMessage( "Invalid syntax. Proper Syntax: /iinfo <player> [ Life | Buffs ]" );
        } // if
        else if ( args.Parameters.Count > 1 )
        {
          TShockAPI.TSPlayer player;
          string playerName, action;
          playerName = args.Parameters[0];

          player = findPlayer( playerName ); 
          if ( player != null ) 
          {
            action = args.Parameters[1].ToUpper();

                 if ( action.Equals( "LIFE"  ) ) { showLifeMana( player ); } // if
            else if ( action.Equals( "BUFFS" ) ) { showBuffs( player );    } // if
            else 
            {
                TerrariaIRC.sendIRCMessage( string.Format( "Invalid action: {0}", action ) );
            } // else
          
          } // if
        } // else if

      } // IrcInfo -------------------------------------------------------------


      // showLifeMana ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      private static void showLifeMana( TShockAPI.TSPlayer player )
      {
        TerrariaIRC.sendIRCMessage( string.Format( "{0}[{1}] Life / Mana: ({2}/{3})",
                                                   player.Name,
                                                   player.IP,
                                                   player.FirstMaxHP,
                                                   player.FirstMaxMP ) );
      } // showLifeMana --------------------------------------------------------


      // showBuffs +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      private static void showBuffs( TShockAPI.TSPlayer player )
      {
        StringBuilder response = new StringBuilder();
        String buffName;
        bool buffFound = false;
        int buffType;
        int buffCount = player.TPlayer.countBuffs();

        for ( int index = 0; index < buffCount; index++ ) {
          buffType = player.TPlayer.buffType[index];
          if ( buffType > 0 ) { 
            buffName = TShock.Utils.GetBuffName( buffType );
            response.Append( buffName ).Append( " (" );
            response.Append( player.TPlayer.buffTime[index] ).Append( ")" );
            if ( index < buffCount-1 ) { response.Append( " | " ); } // if
            buffFound = true;
          } // if
        } // for

        if ( buffFound ) 
        {
          TerrariaIRC.sendIRCMessage( string.Format( "{0} Buffs: {1}", 
                                                     player.Name, response ) );
        } // if
        else 
        {
          TerrariaIRC.sendIRCMessage( string.Format( "{0} has no Buffs", player.Name ) );
        } // else


      } // showBuffs -----------------------------------------------------------


      // findPlayer ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
      private static TShockAPI.TSPlayer findPlayer( string playerName )
      {
        TShockAPI.TSPlayer result = null;

        List<TShockAPI.TSPlayer> playerList = TShockAPI.TShock.Utils.FindPlayer( playerName );
        if ( playerList.Count < 1 )
        {
          TerrariaIRC.sendIRCMessage( string.Format( "Player {0} not found.", playerName ) );
        } // if
        else if ( playerList.Count > 1 )
        {
          TerrariaIRC.sendIRCMessage( string.Format( "Multiple players matched {0}.", playerName ) );
        } // else if
        else
        {
          result = playerList[0];
        } // else

        return result;
      } // findPlayer ----------------------------------------------------------



  } // Commands ----------------------------------------------------------------


} // TerrariaIRC ===============================================================
