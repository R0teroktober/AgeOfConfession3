using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
    public static class DevotionClientState
    {
        public static ICoreClientAPI Capi { get; set; }
        public static bool DevotionActiveClient { get; set; }

        public static bool AppliesTo(EntityPlayer player)
        {
            if (Capi?.World?.Player?.Entity is not EntityPlayer localPlayer)
            {
                return false;
            }

            return DevotionActiveClient
                && localPlayer.EntityId == player.EntityId
                && Capi.World.Player.CameraMode == EnumCameraMode.FirstPerson;
        }
    }

}

