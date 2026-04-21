using Vintagestory.API.MathTools;

namespace AgeOfConfession
{

    public class DevotionSession
    {
        public string PlayerUid { get; set; } = "";

        public bool IsInDevotion { get; set; }
        public bool DevotionIsAnswered { get; set; }

        public string CurrentCommunityId { get; set; } = "";

        public Vec3d StartPosition { get; set; } = new();

        public long LastNothingMessagePulse { get; set; } = -9999;



    }
}

