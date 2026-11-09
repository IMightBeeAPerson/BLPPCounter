using System;
using System.Collections.Generic;

namespace BLPPCounter.Counters
{
    internal enum ClanCaptureStatus
    {
        Success,
        Unranked,
        AlreadyCaptured,
        LoadFailed,
    }

    internal sealed class ClanCaptureResult
    {
        private readonly float[] _clanPpValues;

        public ClanCaptureResult(
            ClanCaptureStatus status,
            float neededPp,
            IEnumerable<float> clanPpValues,
            string owningClan,
            int playerClanId
        )
        {
            Status = status;
            NeededPp = neededPp;

            _clanPpValues = [.. clanPpValues ?? throw new ArgumentNullException(nameof(clanPpValues))];

            OwningClan = owningClan ?? throw new ArgumentNullException(nameof(owningClan));

            PlayerClanId = playerClanId;
        }

        public ClanCaptureStatus Status { get; }

        public float NeededPp { get; }

        public IReadOnlyList<float> ClanPpValues => _clanPpValues;

        public string OwningClan { get; }

        public int PlayerClanId { get; }

        public bool IsSuccessful => Status is ClanCaptureStatus.Success or ClanCaptureStatus.AlreadyCaptured;

        public bool IsCaptured => Status == ClanCaptureStatus.AlreadyCaptured;

        public ClanCaptureResult CreateSnapshot()
        {
            return new ClanCaptureResult(Status, NeededPp, _clanPpValues, OwningClan, PlayerClanId);
        }

        public static ClanCaptureResult Unranked(int playerClanId = -1)
        {
            return new ClanCaptureResult(ClanCaptureStatus.Unranked, -1f, [], "None", playerClanId);
        }

        public static ClanCaptureResult LoadFailed(int playerClanId = -1)
        {
            return new ClanCaptureResult(ClanCaptureStatus.LoadFailed, -1f, [], "None", playerClanId);
        }
    }
}
