using System;
using System.Collections.Generic;
using System.Linq;
using PleaseWork.Helpfuls;
using System.IO;
using System.Text.RegularExpressions;

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
            //TheCounter.ForceLoadMaps();
            string[] files = Directory.GetFiles(HelpfulPaths.PLAYLISTS);
            List<string> names = new List<string>();
            foreach (string file in files)
                names.Add(new string(file.Skip(file.LastIndexOf('.')+1).ToArray()));
            /*foreach (string file in files)
            {
                if (!File.Exists(file)) continue;
                LoadPlaylist(file);
            }*/
        }
        private void LoadPlaylist(string file)
        {
            List<MapSelection> outp = new List<MapSelection>();
            string name = "";
            try 
            {
                string data = File.ReadAllText(file);
                data = new Regex("(?:(?<=:) +(?=\"))|(?: {2,})|\n|\r").Replace(data, "");
                MatchCollection hashs = new Regex(@"(?<=hash...)[A-z0-9]+(?=...levelid)").Matches(data);
                MatchCollection diffs = new Regex(@"(?<=difficulties.. \[{)[^}]+").Matches(data);
                name = new Regex(@"(?<=playlistTitle...)[^,]+(?=.,)").Match(data).Value;
                Regex modeFinder = new Regex(@"(?<=characteristic...)[A-z]+"), diffFinder = new Regex(@"(?<=name...)[A-z]+");
                for (int i = 0; i < diffs.Count; i++)
                {
                    string hash = hashs[i].Value;
                    string mapData = diffs[i].Value;
                    string mode = modeFinder.Match(mapData).Value;
                    string diff = diffFinder.Match(mapData).Value;
                    diff = char.ToUpper(diff[0]) + diff.Substring(1);
                    //Plugin.Log.Info($"hash: {hash}\tmode: {mode}\tdiff: {diff}");
                    TheCounter.Data.TryGetValue(hash, out Map m);
                    if (m != null) outp.Add(new MapSelection(m, diff, mode));
                }
            } 
            catch (Exception e) { Plugin.Log.Info("Error loading playlists\n" + e); }
            Playlists.Add(name, outp.ToArray());
        }
    }
}
