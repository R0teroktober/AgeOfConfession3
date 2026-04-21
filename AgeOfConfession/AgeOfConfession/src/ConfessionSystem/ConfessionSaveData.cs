using System.Collections.Generic;
using ProtoBuf;

namespace AgeOfConfession
{

    [ProtoContract]
    public class ConfessionSaveData
    {
        [ProtoMember(1)]
        public Dictionary<string, BeliefData> BeliefsByCode { get; set; } = new();

        [ProtoMember(2)]
        public Dictionary<string, CommunityRecord> CommunitiesById { get; set; } = new();

        [ProtoMember(3)]
        public Dictionary<string, PlayerDevotionStats> DevotionStatsByPlayerUid { get; set; } = new();
    }

    [ProtoContract]
    public class BeliefData
    {
        [ProtoMember(1)]
        public string Code { get; set; } = "";

        [ProtoMember(2)]
        public string DisplayName { get; set; } = "";

        [ProtoMember(3)]
        public HashSet<string> CommunityIds { get; set; } = new();

        [ProtoMember(4)]
        public string FounderPlayerUid { get; set; } = "";

        [ProtoMember(5)]
        public string FounderPlayerName { get; set; } = "";

        [ProtoMember(6)]
        public double CreatedTotalDays { get; set; }

        [ProtoMember(7)]
        public double BecameEmptyTotalDays { get; set; } = -1;
    }

    [ProtoContract]
    public class CommunityRecord
    {
        [ProtoMember(1)]
        public string CommunityId { get; set; } = "";

        [ProtoMember(2)]
        public string BeliefCode { get; set; } = "";

        [ProtoMember(3)]
        public int X { get; set; }

        [ProtoMember(4)]
        public int Y { get; set; }

        [ProtoMember(5)]
        public int Z { get; set; }

        [ProtoMember(6)]
        public string FounderPlayerUid { get; set; } = "";

        [ProtoMember(7)]
        public string FounderPlayerName { get; set; } = "";

        [ProtoMember(8)]
        public double CreatedTotalDays { get; set; }

        [ProtoMember(9)]
        public int Charge { get; set; }

        [ProtoMember(10)]
        public int MaxCharge { get; set; }

        [ProtoMember(11)]
        public double LastDecayTotalDays { get; set; }
    }

    [ProtoContract]
    public class PlayerDevotionStats
    {
        [ProtoMember(1)]
        public string PlayerUid { get; set; } = "";

        [ProtoMember(2)]
        public int DayIndex { get; set; }

        [ProtoMember(3)]
        public int ChargeContributingPulsesToday { get; set; }
    }

    [ProtoContract]
    public class StartDevotionPacket
    {
    }

    [ProtoContract]
    public class StopDevotionPacket
    {
    }
    [ProtoContract]
    public class InterruptDevotionPacket
    {
    }
    [ProtoContract]
    public class AnsweredDevotionPulsePacket
    {
    }
}