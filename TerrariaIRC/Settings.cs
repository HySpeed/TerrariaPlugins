using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TerrariaIRC
{
  public class Settings : Dictionary<string, string>
  {

    public bool Load( string settingsFile )
    {
      string[] file;
      if ( File.Exists( settingsFile ) )
        file = File.ReadAllLines( settingsFile );
      else
        return false;

      foreach ( var t in file.Where(t => !t.StartsWith("//") 
                                      && !t.StartsWith("#")).Where(t => t.Split('=').Length == 2) ) 
      {
        Add( t.Split('=')[0].ToLower(), t.Split('=')[1] );
      } // for
      
      return CheckKeys();
    } // Load


    private bool CheckKeys()
    {
      var server   = false;
      var port     = false;
      var botName  = false;
      var allowOp  = false;
      var dualChannels  = false;
      var chatChannel   = false;
      var actionChannel = false;
      var useColours    = false;

      foreach ( var pair in this )
      {
        switch ( pair.Key )
        {
          case "server":
            server = true;
            break;

          case "port":
            int sPort;
            if ( int.TryParse( pair.Value, out sPort ) )
              port = true;
            break;
          
          case "botname":
            botName = true;
            break;
          
          case "allowop":
            if ( bool.TryParse( pair.Value, out allowOp ) )
              allowOp = true;
            break;
          
          case "dualchannels":
            if ( bool.TryParse( pair.Value, out dualChannels ) )
              dualChannels = true;
            break;
          
          case "chatchannel":
            chatChannel = true;
            break;

          case "actionchannel":
            actionChannel = true;
            break;

          case "usecolours":
            if ( bool.TryParse( pair.Value, out useColours ) )
              useColours = true;
            break;
          
        } // switch

      } // foreach
  
      return ( server &&  port &&  botName && allowOp && dualChannels && 
               chatChannel && actionChannel && useColours );
    } // CheckKeys
  
  } // Settings

} // TerrariaIRC
