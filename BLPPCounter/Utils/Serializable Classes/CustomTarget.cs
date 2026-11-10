using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Utils.Serializable_Classes
{
    public struct CustomTarget(string name, long id, int rank) : IComparable<CustomTarget>
    {
        [JsonProperty(nameof(Name), Required = Required.DisallowNull)]
        public string Name = name;

        [JsonProperty(nameof(ID), Required = Required.DisallowNull)]
        public long ID = id;

        [JsonProperty(nameof(Rank), Required = Required.DisallowNull)]
        public int Rank = rank; //For now this is only gonna store BL rank.

        internal static async Task<CustomTarget> ConvertToId(string str, APIHandler api)
        {
            if (long.TryParse(str, out long id))
                return await ConvertToId(id, api);
            return await ConvertToIdAlias(str.ToString(), api);
        }

        internal static async Task<CustomTarget> ConvertToId(long id, APIHandler api)
        {
            HttpContent? data =
                await api.CallAPI_Static($"{HelpfulPaths.BLAPI}player/{id}", BLAPI.Throttle).ConfigureAwait(false)
                ?? throw new ArgumentException("The ID provided does not exist.");
            JObject playerData = JObject.Parse(await data.ReadAsStringAsync().ConfigureAwait(false));

            if (!int.TryParse(playerData["rank"]?.ToString(), out int rank))
                throw new ArgumentException($"Rank in api is incorrect, \"{playerData["rank"]}\" is not a number.");

            return new CustomTarget(playerData["name"]?.ToString() ?? "Unknown", id, rank);
        }

        private static async Task<CustomTarget> ConvertToIdAlias(string str, APIHandler api)
        {
            HttpContent? data = await api.CallAPI_Static($"{HelpfulPaths.BLAPI}player/{str.ToLower()}", BLAPI.Throttle)
                .ConfigureAwait(false);

            if (data is not null)
            {
                string strData = await data.ReadAsStringAsync().ConfigureAwait(false);
                JObject parsedData = JObject.Parse(strData);
                if (!long.TryParse(parsedData["id"]?.ToString(), out long id))
                    throw new ArgumentException($"Id in api is incorrect, \"{parsedData["id"]}\" is not a number.");
                if (!int.TryParse(parsedData["rank"]?.ToString(), out int rank))
                    throw new ArgumentException($"Rank in api is incorrect, \"{parsedData["rank"]}\" is not a number.");
                return new CustomTarget(parsedData["name"]?.ToString() ?? "Unknown", id, rank);
            }
            throw new ArgumentException("String given is not an ID or alias.");
        }

        internal static async Task<CustomTarget> ConvertFromRank(string rankStr, PluginConfig pc, APIHandler api)
        {
            if (!int.TryParse(rankStr, out int rank))
                throw new ArgumentException($"That value \"{rankStr}\" cannot be parsed into a number.");
            const int MaxCountPerPage = 50; //the lower this is, the less data will be returned.
            bool usingSS = pc.UseSSRank;
            int page = (rank / MaxCountPerPage) + (rank == MaxCountPerPage ? 0 : 1);
            int index = (rank % MaxCountPerPage) - (rank == MaxCountPerPage ? 0 : 1);
            HttpContent? data =
                await api.CallAPI_Static(
                        usingSS
                            ? string.Format(HelpfulPaths.SSAPI_PLAYER_FILTER, page)
                            : string.Format(HelpfulPaths.BLAPI_PLAYER_FILTER, page, MaxCountPerPage),
                        usingSS ? SSAPI.Throttle : BLAPI.Throttle
                    )
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"Rank #{rank} is not found.");

            JToken? playerData =
                (
                    JToken
                        .Parse(await data.ReadAsStringAsync().ConfigureAwait(false))[usingSS ? "players" : "data"]
                        ?.Children()
                        .Skip(index)
                        .First()
                ) ?? throw new Exception("There was an issue parsing playerData!");

            if (!long.TryParse(playerData["id"]?.ToString(), out long id))
                throw new ArgumentException($"Id in api is incorrect, \"{playerData["id"]}\" is not a number.");

            if (usingSS)
            {
                data = await api.CallAPI_Static(string.Format(HelpfulPaths.BLAPI_USERID, id), BLAPI.Throttle);

                if (data is not null)
                {
                    playerData = JToken.Parse(await data.ReadAsStringAsync().ConfigureAwait(false));
                    rank = (int)(playerData["rank"] ?? 0);
                    usingSS = false;
                }
            }
            return new CustomTarget(playerData["name"]?.ToString() ?? "Unknown", id, usingSS ? 0 : rank);
        }

        // TODO

        public override readonly string ToString()
        {
            return $"{Name}, ID: {ID}, Rank: {Rank}";
        }

        public readonly int CompareTo(CustomTarget other)
        {
            return Rank.CompareTo(other.Rank);
        }
    }
}
