using Microsoft.VisualBasic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using AgeOfConfession;

namespace AgeOfConfession {

    public class BlockCommunityCenter : Block
    {
        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            if (world.Api.Side == EnumAppSide.Server)
            {
                BlockEntityCommunityCenter be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCommunityCenter;

                if (be?.IsBound == true)
                {
                    AgeOfConfessionModSystem confession = world.Api.ModLoader.GetModSystem<AgeOfConfessionModSystem>();
                    confession?.DeregisterCommunity(be.CommunityId);
                }
            }

            base.OnBlockRemoved(world, pos);
        }
    }
}