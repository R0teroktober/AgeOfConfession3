using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AgeOfConfession
{

    public class ModSystemDevotionClient : ModSystem
    {
        private ICoreClientAPI capi = null!;
        private IClientNetworkChannel clientChannel = null!;

        private bool devotionActiveClient;
        private Vec3d devotionStartPosClient;
        private long devotionValidationListenerId = -1;
        private bool sentStopThisFrame;
        private const string DevotionAnimationCode = "devoting";


        private static readonly AssetLocation DevotionAnsweredSound = new("confession:sounds/effect/devotionpulse");
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;

            clientChannel = api.Network.RegisterChannel("confession-devotion").RegisterMessageType(typeof(StartDevotionPacket)).RegisterMessageType(typeof(StopDevotionPacket)).RegisterMessageType(typeof(AnsweredDevotionPulsePacket)).SetMessageHandler<AnsweredDevotionPulsePacket>(OnAnsweredDevotionPulse).RegisterMessageType(typeof(InterruptDevotionPacket)); ;

            capi.Input.RegisterHotKey("confession-devotion","[Confession] Start or stop devotion",GlKeys.KeypadMultiply,HotkeyType.GUIOrOtherControls);

            capi.Input.SetHotKeyHandler("confession-devotion", OnDevotionHotkey);
        }



        private bool OnDevotionHotkey(KeyCombination comb)
        {
            if (!devotionActiveClient)
            {
                StartDevotionClient();
            }
            else
            {
                StopDevotionClient(true);
            }

            return true;
        }

        private void StartDevotionClient()
        {
         
            devotionActiveClient = true;
            sentStopThisFrame = false;

            devotionStartPosClient = capi.World.Player.Entity.Pos.XYZ;

            StartDevotionAnimation();
            if (capi?.World?.Player != null)
            {
                //ApplyDevotionCameraOffset();
            }

            clientChannel.SendPacket(new StartDevotionPacket());

            if (devotionValidationListenerId >= 0)
            {
                capi.Event.UnregisterGameTickListener(devotionValidationListenerId);
            }

            devotionValidationListenerId = capi.Event.RegisterGameTickListener(OnDevotionClientValidationTick, 100);
        }

        private void StopDevotionClient(bool notifyServer)
        {
            if (!devotionActiveClient) return;

            devotionActiveClient = false;

            StopDevotionAnimation();
            if (capi?.World?.Player != null)
            {
                //ResetDevotionCameraOffset();
            }

            if (devotionValidationListenerId >= 0)
            {
                capi.Event.UnregisterGameTickListener(devotionValidationListenerId);
                devotionValidationListenerId = -1;
            }

            if (notifyServer && !sentStopThisFrame)
            {
                sentStopThisFrame = true;
                clientChannel.SendPacket(new StopDevotionPacket());
            }
        }

        private void StartDevotionAnimation()
        {
            if (capi?.World?.Player?.Entity is not EntityPlayer playerEntity)
            {
                return;
            }
            
            playerEntity.StartAnimation(DevotionAnimationCode);
            playerEntity.TpAnimManager?.StartAnimation(DevotionAnimationCode);
            playerEntity.SelfFpAnimManager?.StartAnimation(DevotionAnimationCode);

            var attr = playerEntity.WatchedAttributes;

        }


        private void StopDevotionAnimation()
        {
            if (capi?.World?.Player?.Entity is not EntityPlayer playerEntity)
            {
                return;
            }

            playerEntity.StopAnimation(DevotionAnimationCode);
            playerEntity.TpAnimManager?.StopAnimation(DevotionAnimationCode);
            playerEntity.SelfFpAnimManager?.StopAnimation(DevotionAnimationCode);

            var attr = playerEntity.WatchedAttributes;

        }

        private void OnDevotionClientValidationTick(float dt)
        {
            if (!devotionActiveClient || capi?.World?.Player?.Entity == null)
            {
                StopDevotionClient(false);
                return;
            }

            if (HasPlayerMovedTooFarClient())
            {
                StopDevotionClient(true);
                return;
            }

            if (IsWorldMouseInteractionActive())
            {
                StopDevotionClient(true);
                return;
            }

            if (IsFloorSittingClient())
            {
                StopDevotionClient(true);
                return;
            }
        }
        private bool HasPlayerMovedTooFarClient()
        {
            Vec3d currentPos = capi.World.Player.Entity.Pos.XYZ;

            double dx = currentPos.X - devotionStartPosClient.X;
            double dy = currentPos.Y - devotionStartPosClient.Y;
            double dz = currentPos.Z - devotionStartPosClient.Z;

            return dx * dx + dy * dy + dz * dz > 0.0225;
        }

        private bool IsWorldMouseInteractionActive()
        {
            return capi.Input.InWorldMouseButton.Left|| capi.Input.InWorldMouseButton.Right|| capi.Input.InWorldMouseButton.Middle;
        }

        private void OnAnsweredDevotionPulse(AnsweredDevotionPulsePacket packet)
        {
            capi.World.PlaySoundFor(DevotionAnsweredSound,capi.World.Player,true,1f,0.2f);
        }

     
        private bool IsFloorSittingClient()
        {
            return capi?.World?.Player?.Entity is EntityPlayer playerEntity
                && playerEntity.Controls.FloorSitting;
        }

        public override void Dispose()
        {

            base.Dispose();
        }
    }
}