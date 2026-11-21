using System;
using System.Collections.Generic;
using BLPPCounter.Models.Base;
using Newtonsoft.Json;

namespace BLPPCounter.Models
{
    internal class AccsaberScoreResponse : IScoreModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; } = null!;

        [JsonProperty("userName")]
        public string UserName { get; set; } = null!;

        [JsonIgnore]
        public string PlayerName => UserName;

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; } = null!;

        [JsonProperty("cdnAvatarUrl")]
        public string CdnAvatarUrl { get; set; } = null!;

        [JsonProperty("clan")]
        public AccsaberClan? Clan { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; } = null!;

        [JsonProperty("mapDifficultyId")]
        public Guid MapDifficultyId { get; set; }

        [JsonProperty("mapId")]
        public Guid MapId { get; set; }

        [JsonProperty("beatsaverCode")]
        public string BeatsaverCode { get; set; } = null!;

        [JsonProperty("songHash")]
        public string SongHash { get; set; } = null!;

        [JsonProperty("songName")]
        public string SongName { get; set; } = null!;

        [JsonProperty("songSubName")]
        public string SongSubName { get; set; } = null!;

        [JsonProperty("songAuthor")]
        public string SongAuthor { get; set; } = null!;

        [JsonProperty("mapAuthor")]
        public string MapAuthor { get; set; } = null!;

        [JsonProperty("coverUrl")]
        public string CoverUrl { get; set; } = null!;

        [JsonProperty("cdnCoverUrl")]
        public string CdnCoverUrl { get; set; } = null!;

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; } = null!;

        [JsonProperty("characteristic")]
        public string Characteristic { get; set; } = null!;

        [JsonProperty("categoryId")]
        public Guid CategoryId { get; set; }

        [JsonProperty("complexity")]
        public float Complexity { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("scoreNoMods")]
        public int ScoreNoMods { get; set; }

        [JsonIgnore]
        public int ModifiedScore => ScoreNoMods;

        [JsonProperty("accuracy")]
        public float Accuracy { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("rankWhenSet")]
        public int RankWhenSet { get; set; }

        [JsonProperty("ap")]
        public float Ap { get; set; }

        [JsonIgnore]
        public float LeaderboardPoints => Ap;

        [JsonProperty("weightedAp")]
        public float WeightedAp { get; set; }

        [JsonProperty("blScoreId")]
        public int BlScoreId { get; set; }

        [JsonProperty("ssScoreId")]
        public int SsScoreId { get; set; }

        [JsonProperty("maxCombo")]
        public int MaxCombo { get; set; }

        [JsonProperty("badCuts")]
        public int BadCuts { get; set; }

        [JsonProperty("misses")]
        public int Misses { get; set; }

        [JsonProperty("wallHits")]
        public int WallHits { get; set; }

        [JsonProperty("bombHits")]
        public int BombHits { get; set; }

        [JsonProperty("pauses")]
        public int Pauses { get; set; }

        [JsonProperty("streak115")]
        public int Streak115 { get; set; }

        [JsonProperty("maxStreak115")]
        public int MaxStreak115 { get; set; }

        [JsonProperty("playCount")]
        public int PlayCount { get; set; }

        [JsonProperty("lastPlayedAt")]
        public DateTimeOffset LastPlayedAt { get; set; }

        [JsonProperty("hmd")]
        public string Hmd { get; set; } = null!;

        [JsonProperty("timeSet")]
        public DateTimeOffset TimeSet { get; set; }

        [JsonProperty("reweightDerivative")]
        public bool ReweightDerivative { get; set; }

        [JsonProperty("xpGained")]
        public float XpGained { get; set; }

        [JsonProperty("baseXp")]
        public int BaseXp { get; set; }

        [JsonProperty("bonusXp")]
        public float BonusXp { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("partial")]
        public bool Partial { get; set; }

        [JsonProperty("supersedesReason")]
        public string? SupersedesReason { get; set; }

        [JsonProperty("modifierIds")]
        public List<Guid> ModifierIds { get; set; } = [];

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("myScore")]
        public AccsaberMyScore? MyScore { get; set; }

        [JsonProperty("supporterTier")]
        public string? SupporterTier { get; set; }

        [JsonProperty("skillLevel")]
        public float SkillLevel { get; set; }

        [JsonProperty("metadata")]
        public AccsaberScoreMetadata? Metadata { get; set; }

        [JsonProperty("nps")]
        public float Nps { get; set; }
    }

    internal class AccsaberClan : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("tag")]
        public string Tag { get; set; } = null!;

        [JsonProperty("tagColor")]
        public string TagColor { get; set; } = null!;

        [JsonProperty("primaryColor")]
        public string PrimaryColor { get; set; } = null!;

        [JsonProperty("secondaryColor")]
        public string SecondaryColor { get; set; } = null!;

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; } = null!;

        [JsonProperty("bannerUrl")]
        public string BannerUrl { get; set; } = null!;

        [JsonProperty("equipped")]
        public List<AccsaberClanEquippedItem> Equipped { get; set; } = [];
    }

    internal class AccsaberClanEquippedItem : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("typeId")]
        public Guid TypeId { get; set; }

        [JsonProperty("typeKey")]
        public string TypeKey { get; set; } = null!;

        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("description")]
        public string Description { get; set; } = null!;

        [JsonProperty("iconUrl")]
        public string IconUrl { get; set; } = null!;

        [JsonProperty("value")]
        public string Value { get; set; } = null!;

        [JsonProperty("rarity")]
        public string Rarity { get; set; } = null!;

        [JsonProperty("tradeable")]
        public bool Tradeable { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; }

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("deprecated")]
        public bool Deprecated { get; set; }

        [JsonProperty("stackable")]
        public bool Stackable { get; set; }

        [JsonProperty("welcomeGrant")]
        public bool WelcomeGrant { get; set; }

        [JsonProperty("missionPoolable")]
        public bool MissionPoolable { get; set; }

        [JsonProperty("downloadable")]
        public bool Downloadable { get; set; }

        [JsonProperty("uniquePerUser")]
        public bool UniquePerUser { get; set; }

        [JsonProperty("serialized")]
        public bool Serialized { get; set; }

        [JsonProperty("worth")]
        public int Worth { get; set; }

        [JsonProperty("requirement")]
        public string? Requirement { get; set; }

        [JsonProperty("unlockLevel")]
        public int UnlockLevel { get; set; }

        [JsonProperty("obtainableUntil")]
        public DateTimeOffset? ObtainableUntil { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("variantKey")]
        public string? VariantKey { get; set; }
    }

    internal class AccsaberMyScore : IModel
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("accuracy")]
        public float Accuracy { get; set; }

        [JsonProperty("ap")]
        public float Ap { get; set; }

        [JsonProperty("weightedAp")]
        public float WeightedAp { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("timeSet")]
        public DateTimeOffset TimeSet { get; set; }
    }

    internal class AccsaberScoreMetadata : IModel
    {
        [JsonProperty("bpm")]
        public float Bpm { get; set; }

        [JsonProperty("notes")]
        public int Notes { get; set; }

        [JsonProperty("bombs")]
        public int Bombs { get; set; }

        [JsonProperty("walls")]
        public int Walls { get; set; }

        [JsonProperty("duration")]
        public int Duration { get; set; }
    }
}
