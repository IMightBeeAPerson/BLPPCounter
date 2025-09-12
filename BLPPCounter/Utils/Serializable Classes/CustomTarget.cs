using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BLPPCounter.Utils
{
    public struct CustomTarget : IComparable<CustomTarget>
    {
        [JsonProperty(nameof(Name), Required = Required.DisallowNull)]
        public string Name;
        [JsonProperty(nameof(ID), Required = Required.DisallowNull)]
        public long ID;
        [JsonProperty(nameof(Rank), Required = Required.DisallowNull)]
        public int Rank; //For now this is only gonna store BL rank.

        public CustomTarget(string name, long id, int rank)
        {
            Name = name;
            ID = id;
            Rank = rank;
        }

        public static async Task<CustomTarget> ConvertToId(string str)
        {
            if (long.TryParse(str, out long id))
                return await ConvertToId(id);
            return await ConvertToIdAlias(str.ToString());
        }
        public static async Task<CustomTarget> ConvertToId(long id)
        {
            
            (bool succeeded, HttpContent data) = await APIHandler.CallAPI_Static($"{HelpfulPaths.BLAPI}player/{id}", BLAPI.Throttle).ConfigureAwait(false);
            if (!succeeded) throw new ArgumentException("The ID provided does not exist.");
            JObject playerData = JObject.Parse(await data.ReadAsStringAsync().ConfigureAwait(false));
            if (!int.TryParse(playerData["rank"].ToString(), out int rank))
                throw new ArgumentException($"Rank in api is incorrect, \"{playerData["rank"]}\" is not a number.");
            return new CustomTarget(playerData["name"].ToString(), id, rank);
        }
        private static async Task<CustomTarget> ConvertToIdAlias(string str)
        {
            (bool succeeded, HttpContent data) = await APIHandler.CallAPI_Static($"{HelpfulPaths.BLAPI}player/{str.ToLower()}", BLAPI.Throttle).ConfigureAwait(false);
            if (succeeded)
            {
                string strData = await data.ReadAsStringAsync().ConfigureAwait(false);
                JObject parsedData = JObject.Parse(strData);
                if (!long.TryParse(parsedData["id"].ToString(), out long id))
                    throw new ArgumentException($"Id in api is incorrect, \"{parsedData["id"]}\" is not a number.");
                if (!int.TryParse(parsedData["rank"].ToString(), out int rank))
                    throw new ArgumentException($"Rank in api is incorrect, \"{parsedData["rank"]}\" is not a number.");
                return new CustomTarget(parsedData["name"].ToString(), id, rank);
            }
            throw new ArgumentException("String given is not an ID or alias.");
        }
        public static async Task<CustomTarget> ConvertFromRank(string rankStr)
        {
            if (!int.TryParse(rankStr, out int rank))
                throw new ArgumentException($"That value \"{rankStr}\" cannot be parsed into a number.");
            const int MaxCountPerPage = 50; //the lower this is, the less data will be returned.
            bool usingSS = PluginConfig.Instance.UseSSRank;
            int page = rank / MaxCountPerPage + (rank == MaxCountPerPage ? 0 : 1);
            int index = rank % MaxCountPerPage - (rank == MaxCountPerPage ? 0 : 1);
            (bool succeeded, HttpContent data) = await APIHandler.CallAPI_Static(
                usingSS ? string.Format(HelpfulPaths.SSAPI_PLAYER_FILTER, page) :
                string.Format(HelpfulPaths.BLAPI_PLAYER_FILTER, page, MaxCountPerPage),
                usingSS ? SSAPI.Throttle : BLAPI.Throttle
                ).ConfigureAwait(false);
            if (!succeeded)
                throw new ArgumentException($"Rank #{rank} is not found.");
            JToken playerData = JToken.Parse(await data.ReadAsStringAsync().ConfigureAwait(false))[usingSS ? "players" : "data"].Children().Skip(index).First();
            if (!long.TryParse(playerData["id"].ToString(), out long id))
                throw new ArgumentException($"Id in api is incorrect, \"{playerData["id"]}\" is not a number.");
            if (usingSS)
            {
                (succeeded, data) = await APIHandler.CallAPI_Static(string.Format(HelpfulPaths.BLAPI_USERID, id), BLAPI.Throttle);
                if (succeeded)
                {
                    playerData = JToken.Parse(await data.ReadAsStringAsync().ConfigureAwait(false));
                    rank = (int)playerData["rank"];
                    usingSS = false;
                }
            }
            return new CustomTarget(playerData["name"].ToString(), id, usingSS ? 0 : rank);
        }
        public override string ToString() => $"{Name}, ID: {ID}, Rank: {Rank}";
        public int CompareTo(CustomTarget other) => Rank.CompareTo(other.Rank);
    }
}
