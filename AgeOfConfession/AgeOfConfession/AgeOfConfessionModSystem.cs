using AgeOfConfession;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace AgeOfConfession
{
    public class AgeOfConfessionModSystem : ModSystem
    {
        //public static ConfessionConfig Config { get; private set; }
        private ConfessionConfig config = new();
        public ConfessionConfig Config => config;

        private const string SaveDataKey = "confession:saveData";
        private ConfessionSaveData saveData = new();
        public ConfessionSaveData SaveData => saveData;

        public ICoreServerAPI sapi;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);



            api.RegisterBlockClass("CommunityCenter", typeof(BlockCommunityCenter));
            api.RegisterBlockEntityClass("CommunityCenter", typeof(BlockEntityCommunityCenter));


            config = api.LoadModConfig<ConfessionConfig>("confession-config.json");
            if (config == null)
            {
                config = new ConfessionConfig()
                {
                    StartCharge = 540,
                    MaxCharge = 1980,
                    CommunityExclusionRadius = 100,
                    InfluenceRadius = 10,
                    BaseChargeGain = 3,
                    DecayRate = 3,
                    EmptyBeliefDeletionDays = 365,
                    EffectiveDevotionPulsesPerDay = 8,
                    TemporalStabilityGainByTier = new float[] { 0.0075f, 0.015f, 0.025f, 0.04f },
                    HungerReductionByTier = new float[] { 0.10f, 0.25f, 0.40f, 0.50f },
                    HealingGainByTier = new float[] { 0.1f, 0.2f, 0.4f, 0.6f },
                    AreaDamageByTier = new float[] { 1f, 2f, 4f, 5f },


                };

                api.StoreModConfig(Config, "ageofconfession.json");
                api.Logger.Notification("[AgeOfConfession] Default config created.");
            }
            else
            {
                api.Logger.Notification("[AgeOfConfession] Config loaded successfully.");
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            sapi.Event.SaveGameLoaded += OnSaveGameLoading;
            sapi.Event.GameWorldSave += OnGameWorldSaving;

            #region chatCommands
            var cmdapi = api.ChatCommands;
            var parsers = api.ChatCommands.Parsers;


            cmdapi
                .Create("confession")
                .WithDescription("AgeOfConfession commands")
                .RequiresPrivilege(Privilege.chat)

                .BeginSubCommand("createBelief")
                    .WithDescription("Create a new belief")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.Word("name"))
                    .HandleWith(OnCreateBelief)
                .EndSubCommand()


                .BeginSubCommand("ListBeliefs")
                    .WithDescription("List all beliefs")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnListBeliefs)
                .EndSubCommand()

                .BeginSubCommand("bindCommunity")
                    .RequiresPrivilege(Privilege.useblock)
                    .WithDescription("Bind the targeted unbound community center to a belief")
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.Word("belief"))
                    .HandleWith(OnBindCommunity)
                .EndSubCommand()

                .BeginSubCommand("admin")
                    .RequiresPrivilege(Privilege.controlserver)
                    .BeginSubCommand("deleteCommunity")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithDescription("Delete a community and turn its block back to unbound")
                        .WithArgs(api.ChatCommands.Parsers.Word("id"))
                        .HandleWith(OnAdminDeleteCommunity)
                     .EndSubCommand()
                        .BeginSubCommand("deleteBelief")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithDescription("Delete a belief and unbind all its communities")
                        .WithArgs(api.ChatCommands.Parsers.Word("name"))
                        .HandleWith(OnAdminDeleteBelief)
                     .EndSubCommand()
                .EndSubCommand();

            #endregion


            #region tickListener
            api.Event.RegisterGameTickListener(OnDecayTick, 30000);
            #endregion
        }
        private void OnSaveGameLoading()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData(SaveDataKey);

            saveData = data == null
                ? new ConfessionSaveData()
                : SerializerUtil.Deserialize<ConfessionSaveData>(data);
        }

        private void OnGameWorldSaving()
        {
            sapi.WorldManager.SaveGame.StoreData(
                SaveDataKey,
                SerializerUtil.Serialize(saveData)
            );
        }

        public void Save()
        {
            sapi.WorldManager.SaveGame.StoreData(
                SaveDataKey,
                SerializerUtil.Serialize(saveData)
            );
        }

        private TextCommandResult OnCreateBelief(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null)
            {
                return TextCommandResult.Error("Only players can create beliefs.");
            }
            if (HasActiveBeliefFoundedBy(player.PlayerUID))
            {
                return TextCommandResult.Error("You already founded an active belief.");
            }
            string displayName = (args[0] as string)?.Trim() ?? "";
            string code = NormalizeBeliefCode(displayName);

            if (string.IsNullOrWhiteSpace(code))
            {
                return TextCommandResult.Error("Invalid belief name.");
            }

            if (saveData.BeliefsByCode.ContainsKey(code))
            {
                return TextCommandResult.Error($"A belief named '{displayName}' already exists.");
            }


            BeliefData belief = new()
            {
                Code = code,
                DisplayName = displayName,
                FounderPlayerUid = player?.PlayerUID ?? "",
                FounderPlayerName = player?.PlayerName ?? "",
                CreatedTotalDays = sapi.World.Calendar.TotalDays,
                BecameEmptyTotalDays = sapi.World.Calendar.TotalDays
            };

            saveData.BeliefsByCode[code] = belief;
            Save();

            return TextCommandResult.Success($"Belief '{displayName}' has been created.");
        }

        private TextCommandResult OnListBeliefs(TextCommandCallingArgs args)
        {
            bool showAdminInfo = args.Caller.Player is IServerPlayer player && player.HasPrivilege(Privilege.controlserver);
            if (saveData.BeliefsByCode.Count == 0)
            {
                return TextCommandResult.Success("No beliefs exist yet.");
            }


            StringBuilder sb = new("Existing beliefs:");

            foreach (BeliefData belief in saveData.BeliefsByCode.Values.OrderBy(b => b.DisplayName))
            {
                sb.AppendLine();
                sb.Append("- ");
                sb.Append(belief.DisplayName);
                sb.Append(" (");
                sb.Append(belief.CommunityIds.Count);
                sb.Append(belief.CommunityIds.Count == 1 ? " community" : " communities");
                sb.Append(")");

                if (!showAdminInfo || belief.CommunityIds.Count == 0)
                {
                    var countdown = sapi.World.Calendar.TotalDays - belief.BecameEmptyTotalDays;


                    sb.Append($", vanishes: {countdown:0}/{config.EmptyBeliefDeletionDays} ingame days ");
                    continue;
                }
                sb.Append(", ");




                List<string> communityInfos = new();

                foreach (string communityId in belief.CommunityIds)
                {
                    if (!saveData.CommunitiesById.TryGetValue(communityId, out CommunityRecord record))
                    {
                        communityInfos.Add($"{communityId} [missing record]");
                        continue;
                    }

                    BlockPos worldPos = new(record.X, record.Y, record.Z);
                    Vec3i mapPos = worldPos.ToLocalPosition(sapi);

                    communityInfos.Add(
                        $"[id: {communityId} pos: {mapPos.X},{mapPos.Y},{mapPos.Z} charge: {record.Charge}/{record.MaxCharge}]"
                    );
                }

                sb.Append(string.Join(", ", communityInfos));

            }

            return TextCommandResult.Success(sb.ToString());
        }
        private TextCommandResult OnBindCommunity(TextCommandCallingArgs args)
        {
            string displayName = (args[0] as string)?.Trim() ?? "";
            string beliefCode = NormalizeBeliefCode(displayName);

            if (!saveData.BeliefsByCode.TryGetValue(beliefCode, out BeliefData belief))
            {
                return TextCommandResult.Error($"Belief '{displayName}' does not exist.");
            }

            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null)
            {
                return TextCommandResult.Error("Only players can bind communities.");
            }

            if (HasActiveCommunityFoundedBy(player.PlayerUID))
            {
                return TextCommandResult.Error("You already founded an active community.");
            }

            BlockSelection selection = player.CurrentBlockSelection;
            if (selection == null)
            {
                return TextCommandResult.Error("You must look at a community center.");
            }

            BlockPos pos = selection.Position;
            Block block = sapi.World.BlockAccessor.GetBlock(pos);

            if (block.Code.Domain != "confession" || !block.Code.Path.StartsWith("communityblock-"))
            {
                return TextCommandResult.Error("The targeted block is not a community center.");
            }

            if (block.Variant?["state"] != "unbound")
            {
                return TextCommandResult.Error("This community center is already bound.");
            }

            BlockEntityCommunityCenter be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCommunityCenter;
            if (be == null || !be.CanBind())
            {
                return TextCommandResult.Error("This community center is in an invalid state.");
            }

            if (IsAnotherCommunityTooClose(pos, out int distance))
            {
                return TextCommandResult.Error($"Another community is too close to this place ({distance} blocks).");
            }

            string communityId = GetCommunityId(pos);

            be.Bind(
                beliefCode,
                communityId,
                config.StartCharge,
                config.MaxCharge,
                sapi.World.Calendar.TotalDays
            );

            Block boundBlock = GetBoundVariant(block);
            if (boundBlock == null || boundBlock.Id == 0)
            {
                return TextCommandResult.Error("Could not find the bound block variant.");
            }

            sapi.World.BlockAccessor.ExchangeBlock(boundBlock.BlockId, pos);

            CommunityRecord record = new()
            {
                CommunityId = communityId,
                BeliefCode = beliefCode,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                FounderPlayerUid = player.PlayerUID,
                FounderPlayerName = player.PlayerName,
                CreatedTotalDays = sapi.World.Calendar.TotalDays,
                Charge = config.StartCharge,
                MaxCharge = config.MaxCharge,
                LastDecayTotalDays = sapi.World.Calendar.TotalDays
            };

            saveData.CommunitiesById[communityId] = record;

            if (!belief.CommunityIds.Contains(communityId))
            {
                belief.CommunityIds.Add(communityId);
            }

            belief.BecameEmptyTotalDays = -1;

            Save();

            be.MarkDirty(true);

            return TextCommandResult.Success($"This community center has been bound to the belief '{belief.DisplayName}'.");
        }



        private void DeleteCommunityAndUnbindBlock(string communityId, bool save = true)
        {
            if (!saveData.CommunitiesById.TryGetValue(communityId, out CommunityRecord record))
            {
                return;
            }

            BlockPos pos = new(record.X, record.Y, record.Z);

            BlockEntityCommunityCenter be =
                sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCommunityCenter;

            Block block = sapi.World.BlockAccessor.GetBlock(pos);

            if (block != null && IsCommunityCenterBlock(block))
            {
                if (be != null && be.CommunityId == communityId)
                {
                    be.ResetToUnbound();
                }

                Block unboundBlock = GetUnboundVariant(block);

                if (unboundBlock != null && unboundBlock.Id != 0)
                {
                    sapi.World.BlockAccessor.ExchangeBlock(unboundBlock.BlockId, pos);
                }

                be?.MarkDirty(true);
            }

            DeregisterCommunity(communityId, false);

            if (save)
            {
                Save();
            }
        }

        public bool DeregisterCommunity(string communityId, bool save = true)
        {
            if (string.IsNullOrEmpty(communityId)) return false;

            if (!saveData.CommunitiesById.TryGetValue(communityId, out CommunityRecord record))
            {
                return false;
            }

            saveData.CommunitiesById.Remove(communityId);

            if (saveData.BeliefsByCode.TryGetValue(record.BeliefCode, out BeliefData belief))
            {
                belief.CommunityIds.Remove(communityId);

                if (belief.CommunityIds.Count == 0)
                {
                    belief.BecameEmptyTotalDays = sapi.World.Calendar.TotalDays;
                }
            }

            if (save)
            {
                Save();
            }

            return true;
        }


        public void SyncLoadedCommunityCenter(CommunityRecord record)
        {
            BlockPos pos = new(record.X, record.Y, record.Z);

            if (sapi.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityCommunityCenter be)
            {
                return;
            }

            if (!be.IsBound || be.CommunityId != record.CommunityId)
            {
                return;
            }

            be.SyncChargeFromSystem(record.Charge, record.MaxCharge, record.LastDecayTotalDays);
        }
        #region helpers
        private static string NormalizeBeliefCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            string trimmed = input.Trim().ToLowerInvariant();
            StringBuilder sb = new();

            foreach (char c in trimmed)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private string GetCommunityId(BlockPos pos)
        {
            return $"{pos.X}{pos.Z}";
        }

        private bool HasActiveCommunityFoundedBy(string playerUid)
        {
            return saveData.CommunitiesById.Values.Any(record =>
                record.FounderPlayerUid == playerUid
            );
        }

        private bool HasActiveBeliefFoundedBy(string playerUid)
        {
            return saveData.BeliefsByCode.Values.Any(belief =>
                belief.FounderPlayerUid == playerUid
            );
        }

        private bool IsAnotherCommunityTooClose(BlockPos pos, out int distance)
        {
            distance = int.MaxValue;

            foreach (CommunityRecord record in saveData.CommunitiesById.Values)
            {
                int dx = record.X - pos.X;
                int dz = record.Z - pos.Z;

                double dist = Math.Sqrt(dx * dx + dz * dz);

                if (dist < config.CommunityExclusionRadius)
                {
                    distance = (int)Math.Round(dist);
                    return true;
                }
            }

            return false;
        }

        private Block GetBoundVariant(Block currentBlock)
        {
            string currentPath = currentBlock.Code.Path;

            string boundPath = currentPath.Replace("-unbound-", "-bound-");

            AssetLocation boundCode = new(currentBlock.Code.Domain, boundPath);

            return sapi.World.GetBlock(boundCode);
        }

        private TextCommandResult OnAdminDeleteCommunity(TextCommandCallingArgs args)
        {
            string communityId = (args[0] as string)?.Trim() ?? "";

            if (string.IsNullOrEmpty(communityId))
            {
                return TextCommandResult.Error("Invalid community id.");
            }

            if (!saveData.CommunitiesById.ContainsKey(communityId))
            {
                return TextCommandResult.Error($"Community '{communityId}' does not exist.");
            }

            DeleteCommunityAndUnbindBlock(communityId);

            return TextCommandResult.Success($"Community '{communityId}' has been deleted.");
        }

        private TextCommandResult OnAdminDeleteBelief(TextCommandCallingArgs args)
        {
            string displayName = (args[0] as string)?.Trim() ?? "";
            string beliefCode = NormalizeBeliefCode(displayName);

            if (!saveData.BeliefsByCode.TryGetValue(beliefCode, out BeliefData belief))
            {
                return TextCommandResult.Error($"Belief '{displayName}' does not exist.");
            }

            List<string> communityIds = belief.CommunityIds.ToList();

            foreach (string communityId in communityIds)
            {
                DeleteCommunityAndUnbindBlock(communityId);
            }

            saveData.BeliefsByCode.Remove(beliefCode);
            Save();

            return TextCommandResult.Success($"Belief '{belief.DisplayName}' and all its communities have been deleted.");
        }

        public bool IsCommunityCenterBlock(Block block)
        {
            return block.Code.Domain == "confession"
                && block.Code.Path.StartsWith("communityblock-");
        }

        private Block GetUnboundVariant(Block currentBlock)
        {
            string currentPath = currentBlock.Code.Path;
            string unboundPath = currentPath.Replace("-bound-", "-unbound-");

            AssetLocation unboundCode = new(currentBlock.Code.Domain, unboundPath);

            return sapi.World.GetBlock(unboundCode);
        }

        #endregion

        #region DecaySystem
        private int GetDecayForCommunityCount(int communityCount)
        {
            if (communityCount <= 0) return 0;

            if (communityCount <= 3)
            {
                return config.DecayRate;
            }

            if (communityCount <= 6)
            {
                return Math.Max(1, config.DecayRate * 2 / 3);
            }

            return Math.Max(1, config.DecayRate / 3);
        }

        private bool ProcessEmptyBeliefCleanup(double nowTotalDays)
        {
            bool changed = false;

            foreach (BeliefData belief in saveData.BeliefsByCode.Values.ToList())
            {
                if (belief.CommunityIds.Count > 0)
                {
                    if (belief.BecameEmptyTotalDays >= 0)
                    {
                        belief.BecameEmptyTotalDays = -1;
                        changed = true;
                    }

                    continue;
                }

                if (belief.BecameEmptyTotalDays < 0)
                {
                    belief.BecameEmptyTotalDays = nowTotalDays;
                    changed = true;
                    continue;
                }

                if (nowTotalDays - belief.BecameEmptyTotalDays >= config.EmptyBeliefDeletionDays)
                {
                    saveData.BeliefsByCode.Remove(belief.Code);
                    changed = true;
                }
            }

            return changed;
        }
        private void OnDecayTick(float dt)
        {
            if (sapi?.World?.Calendar == null) return;
            if (saveData == null) return;
            if (config == null) return;

            bool changed = false;
            double nowTotalDays = sapi.World.Calendar.TotalDays;

            foreach (CommunityRecord record in saveData.CommunitiesById.Values.ToList())
            {
                if (!saveData.BeliefsByCode.TryGetValue(record.BeliefCode, out BeliefData belief))
                {
                    DeleteCommunityAndUnbindBlock(record.CommunityId, false);
                    changed = true;
                    continue;
                }

                if (record.MaxCharge <= 0)
                {
                    record.MaxCharge = config.MaxCharge;
                    changed = true;
                }

                if (record.Charge <= 0)
                {
                    record.Charge = config.StartCharge;
                    record.LastDecayTotalDays = nowTotalDays;
                    changed = true;
                }

                int elapsedDays = (int)Math.Floor(nowTotalDays - record.LastDecayTotalDays);
                if (elapsedDays <= 0) continue;

                int decayPerDay = GetDecayForCommunityCount(belief.CommunityIds.Count);
                int totalDecay = decayPerDay * elapsedDays;

                record.Charge = Math.Max(0, record.Charge - totalDecay);
                record.LastDecayTotalDays += elapsedDays;

                if (record.Charge <= 0)
                {
                    DeleteCommunityAndUnbindBlock(record.CommunityId, false);
                    changed = true;
                    continue;
                }

                SyncLoadedCommunityCenter(record);
                changed = true;
            }

            if (ProcessEmptyBeliefCleanup(nowTotalDays))
            {
                changed = true;
            }

            if (changed)
            {
                Save();
            }
        }
        #endregion
    }
}

