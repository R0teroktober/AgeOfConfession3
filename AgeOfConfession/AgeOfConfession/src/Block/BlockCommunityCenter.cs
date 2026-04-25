using Microsoft.VisualBasic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using AgeOfConfession;

namespace AgeOfConfession {

    public class BlockCommunityCenter : Block
    {

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            string style = Variant["style"];
            string firstCodePart = Code.FirstCodePart();

            Block dropBlock = world.GetBlock(new AssetLocation(Code.Domain, $"{firstCodePart}-{style}-unbound-ns"));
            if (dropBlock == null) { dropBlock = world.GetBlock(new AssetLocation(Code.Domain, $"{firstCodePart}-{style}-unbound-north")); }

            if (dropBlock == null || dropBlock.Id == 0)
            {
                return new ItemStack[0];
            }

            return new[]
            {
                new ItemStack(dropBlock)
            };
        }
        public override ItemStack OnPickBlock(IWorldAccessor world,BlockPos pos)
        {
            string style = Variant["style"];
            string firstCodePart = Code.FirstCodePart();

            Block pickBlock = world.GetBlock(new AssetLocation(Code.Domain,$"{firstCodePart}-{style}-unbound-ns"));
            if (pickBlock == null) { pickBlock = world.GetBlock(new AssetLocation(Code.Domain, $"{firstCodePart}-{style}-unbound-north")); }

            return new ItemStack(pickBlock ?? this);
        }
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