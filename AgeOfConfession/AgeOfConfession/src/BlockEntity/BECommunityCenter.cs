using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AgeOfConfession {

    public class BlockEntityCommunityCenter : BlockEntity
    {
        public bool IsBound { get; private set; }

        public string BeliefCode { get; private set; } = "";
        public string CommunityId { get; private set; } = "";

        public int Charge { get; private set; }
        public int MaxCharge { get; private set; } = 1980;

        public double LastDecayTotalDays { get; private set; }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            IsBound = Block?.Variant?["state"] == "bound";
        }

        public bool CanBind()
        {
            return !IsBound
                && string.IsNullOrEmpty(BeliefCode)
                && string.IsNullOrEmpty(CommunityId);
        }

        public void Bind(string beliefCode, string communityId, int startCharge, int maxCharge, double currentTotalDays)
        {
            IsBound = true;
            BeliefCode = beliefCode ?? "";
            CommunityId = communityId ?? "";
            Charge = startCharge;
            MaxCharge = maxCharge;
            LastDecayTotalDays = currentTotalDays;

            MarkDirty(true);
        }

        public override void OnExchanged(Block block)
        {
            base.OnExchanged(block);

            IsBound = block?.Variant?["state"] == "bound";

            MarkDirty(true);
        }

        public void ResetToUnbound()
        {
            IsBound = false;
            BeliefCode = "";
            CommunityId = "";
            Charge = 0;
            LastDecayTotalDays = 0;

            MarkDirty(true);
        }

        public void AddCharge(int amount)
        {
            if (!IsBound || amount <= 0) return;

            Charge = Math.Min(MaxCharge, Charge + amount);
            MarkDirty(true);
        }

        public void ReduceCharge(int amount)
        {
            if (!IsBound || amount <= 0) return;

            Charge = Math.Max(0, Charge - amount);
            MarkDirty(true);
        }

        public int GetChargeTier()
        {
            if (!IsBound || Charge <= 0) return 0;
            if (Charge >= 1620) return 4;
            if (Charge >= 1260) return 3;
            if (Charge >= 900) return 2;
            return 1;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("isBound", IsBound);
            tree.SetString("beliefCode", BeliefCode);
            tree.SetString("communityId", CommunityId);
            tree.SetInt("charge", Charge);
            tree.SetInt("maxCharge", MaxCharge);
            tree.SetDouble("lastDecayTotalDays", LastDecayTotalDays);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            IsBound = tree.GetBool("isBound", false);
            BeliefCode = tree.GetString("beliefCode", "");
            CommunityId = tree.GetString("communityId", "");
            Charge = tree.GetInt("charge", 0);
            MaxCharge = tree.GetInt("maxCharge", 1980);
            LastDecayTotalDays = tree.GetDouble("lastDecayTotalDays", 0);
        }
        public void SyncChargeFromSystem(int charge, int maxCharge, double lastDecayTotalDays)
        {
            MaxCharge = maxCharge;
            Charge = Math.Max(0, Math.Min(charge, MaxCharge));
            LastDecayTotalDays = lastDecayTotalDays;

            MarkDirty(true);
        }
        public int GetChargeTier(int charge, ConfessionConfig config)
        {
            if (charge <= 0) return 0;

            int range = Math.Max(1, (config.MaxCharge - config.StartCharge) / 4);

            int tier2 = config.StartCharge + range;
            int tier3 = config.StartCharge + range * 2;
            int tier4 = config.StartCharge + range * 3;

            if (charge >= tier4) return 4;
            if (charge >= tier3) return 3;
            if (charge >= tier2) return 2;

            return 1;
        }
    }

}