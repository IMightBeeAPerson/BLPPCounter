using System;
using System.Collections.Generic;
using System.Linq;
using PleaseWork.Helpfuls;
using System.IO;
using Newtonsoft.Json.Linq;

namespace PleaseWork.Utils
{
    public class PlaylistLoader
    {
        public static PlaylistLoader Instance { get; private set; }
        public string[] Names { get; private set; }
        public Dictionary<string, MapSelection[]> Playlists { get; private set; }
        public PlaylistLoader() { 
            Instance = this; 
            Playlists = new Dictionary<string, MapSelection[]>();
            LoadPlaylists();
        }

        private void LoadPlaylists()
        {
            string[] files = Directory.GetFiles(HelpfulPaths.PLAYLISTS);
            List<string> names = new List<string>();
            foreach (string file in files)
                names.Add(new string(file.Skip(file.LastIndexOf('.')+1).ToArray()));
            foreach (string file in files)
            {
                if (!File.Exists(file)) continue;
                LoadPlaylist(file);
            }
        }
        private void LoadPlaylist(string file)
        {
            List<MapSelection> outp = new List<MapSelection>();
            string name = "";
            try 
            {
                JToken data = JToken.Parse(File.ReadAllText(file));
                JEnumerable<JToken> songs = data["songs"].Children();
                name = data["playlistTitle"].ToString();
                foreach (JToken song in songs)
                    if (TheCounter.Data.TryGetValue(song["hash"].ToString(), out Map m)) 
                    {
                        JEnumerable<JToken> diffs = song["difficulties"].Children();
                        foreach (JToken diff in diffs)
                            outp.Add(new MapSelection(m, $"{char.ToUpper(diff["Name"].ToString()[0])}{diff["Name"].ToString().Substring(1)}", diff["characteristic"].ToString()));
                    }
            } 
            catch (Exception e) { Plugin.Log.Info("Error loading playlists\n" + e); }
            Playlists.Add(name, outp.ToArray());
        }
    }
}
