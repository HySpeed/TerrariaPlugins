using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TerrariaIRC
{
  internal class Settings : Dictionary<string, string>
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
        Add(t.Split('=')[0].ToLower(), t.Split('=')[1]);
      } // for

      return CheckKeys();
    } // Load


    private bool CheckKeys()
    {
      var server  = false;
      var port    = false;
      var allowop = false;
      var channel = false;
      var name    = false;
      foreach (var pair in this)
      {
        switch (pair.Key)
        {
          case "server":
            server = true;
            break;
          case "port":
            int s;
            if (int.TryParse(pair.Value, out s))
              port = true;
            break;
          case "allowop":
            if (bool.TryParse(pair.Value, out allowop))
              allowop = true;
            break;
          case "channel":
            channel = true;
            break;
          case "botname":
            name = true;
            break;
        } // switch
      } // foreach
  
      return (server && port && allowop && channel && name);
    } // CheckKeys
  
  } // Settings

} // TerrariaIRC
