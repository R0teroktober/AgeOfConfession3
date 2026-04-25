using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;



namespace AgeOfConfession {

    public class ModSystemDevotion : ModSystem
    {
        private const int DevotionPulseIntervalMs = 15000;

        
        private ICoreServerAPI sapi = null!;
        private AgeOfConfessionModSystem confession = null!;
        private IServerNetworkChannel serverChannel = null!;

        private readonly Dictionary<string, DevotionSession> devotionSessionsByPlayerUid = new();

        private long devotionPulseIndex;

        private const string DevotionHungerStatCode = "ageofconfession-devotion";


        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;
            confession = api.ModLoader.GetModSystem<AgeOfConfessionModSystem>();

            serverChannel = api.Network.RegisterChannel("confession-devotion").RegisterMessageType(typeof(StartDevotionPacket)).RegisterMessageType(typeof(StopDevotionPacket)).SetMessageHandler<StartDevotionPacket>(OnStartDevotionPacket).SetMessageHandler<StopDevotionPacket>(OnStopDevotionPacket).RegisterMessageType(typeof(AnsweredDevotionPulsePacket));

            #region tickListener
            api.Event.RegisterGameTickListener(OnDevotionPulse, DevotionPulseIntervalMs);
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            #endregion
        }
        private void OnStartDevotionPacket(IServerPlayer fromPlayer, StartDevotionPacket packet)
        {
            if (fromPlayer?.Entity == null) return;
           
            StartDevotion(fromPlayer);
        }

        private void OnStopDevotionPacket(IServerPlayer fromPlayer, StopDevotionPacket packet)
        {
            if (fromPlayer == null) return;
           
            StopDevotion(fromPlayer);
        }


        private class AnsweredDevotion
        {
            public IServerPlayer Player { get; set; } = null!;
            public DevotionSession Session { get; set; } = null!;
            public CommunityRecord Community { get; set; } = null!;
        }

      

     
        public void StartDevotion(IServerPlayer player)
        {
            Vec3d startPosition = new(player.Entity.Pos.X,player.Entity.Pos.Y,player.Entity.Pos.Z);

            devotionSessionsByPlayerUid[player.PlayerUID] = new DevotionSession{PlayerUid = player.PlayerUID,IsInDevotion = true,DevotionIsAnswered = false,CurrentCommunityId = "",StartPosition = startPosition,};
        }

        public bool StopDevotion(IServerPlayer player)
        {
            if (player == null) return false;
           
            RemoveDevotionHungerModifier(player);

            return devotionSessionsByPlayerUid.Remove(player.PlayerUID);
        }

        private void OnDevotionPulse(float dt)
        {
            if (sapi?.World == null || confession?.SaveData == null || confession.Config == null) return;
            

            devotionPulseIndex++;

            List<AnsweredDevotion> answeredDevotions = new();

            foreach (DevotionSession session in devotionSessionsByPlayerUid.Values.ToList())
            {
                if (!session.IsInDevotion)
                {
                    devotionSessionsByPlayerUid.Remove(session.PlayerUid);
                    continue;
                }

                IServerPlayer player = GetOnlineServerPlayer(session.PlayerUid);

                if (player == null || player.Entity == null || player.ConnectionState != EnumClientState.Playing)
                {
                    devotionSessionsByPlayerUid.Remove(session.PlayerUid);
                    continue;
                }
                if (!player.Entity.Alive)
                {
                    StopDevotion(player);
                    continue;
                }

                if (HasPlayerMovedTooFar(player, session))
                {
                    StopDevotion(player);
                    continue;
                }

                CommunityRecord community = FindValidCommunityInRange(player);

                if (community == null)
                {
                    HandleUnansweredDevotion(player, session);
                    continue;
                }

                HandleAnsweredDevotion(player, session, community);
                serverChannel.SendPacket(new AnsweredDevotionPulsePacket(), player);

                ApplyPersonalDevotionRewards(player, community);

                answeredDevotions.Add(new AnsweredDevotion{Player = player,Session = session,Community = community});
            }
            ProcessAreaDamage(answeredDevotions);
            ProcessChargeGain(answeredDevotions);

        }

        private void ProcessAreaDamage(List<AnsweredDevotion> answeredDevotions)
        {
            if (answeredDevotions.Count == 0) return;
           
            foreach (IGrouping<string, AnsweredDevotion> group in answeredDevotions.GroupBy(entry => entry.Community.CommunityId))
            {
                CommunityRecord community = group.First().Community;

                int tier = GetChargeTier(community);
                if (tier <= 0) continue;
               
                float baseDamage = GetTierValue(
                    confession.Config.AreaDamageByTier,
                    tier,
                    0f
                );

                if (baseDamage <= 0f) continue;
              
                int answeredPlayerCount = group.Select(entry => entry.Player.PlayerUID).Distinct().Count();
                float multiplier = CalculateAreaDamagePlayerMultiplier(answeredPlayerCount);
                float finalDamage = baseDamage * multiplier;

                ApplyAreaDamage(community, finalDamage);
            }
        }


        private float CalculateAreaDamagePlayerMultiplier(int playerCount)
        {
            if (playerCount <= 1) return 1f;
           
            const float maxMultiplier = 2.0f;
            const float scaling = 0.35f;

            return 1f + (maxMultiplier - 1f) * (1f - MathF.Exp(-scaling * (playerCount - 1)));
        }

        private void ApplyAreaDamage(CommunityRecord community, float damage)
        {
            if (damage <= 0f) return;
            

            const int maxTargets = 20;

            Vec3d center = new(community.X + 0.5,community.Y + 0.5,community.Z + 0.5);

            float radius = Math.Max(1, confession.Config.InfluenceRadius);

            int matchedTargets = 0;

            Entity[] targets = sapi.World.GetEntitiesAround(center,radius,radius,entity =>
                {
                    if (matchedTargets >= maxTargets) return false;

                    if (!IsValidAreaDamageTarget(entity)) return false;
                    
                    matchedTargets++;
                    return true;
                }
            );

            foreach (Entity target in targets)
            {
                target.ReceiveDamage(new DamageSource{Source = EnumDamageSource.Internal,Type = EnumDamageType.BluntAttack,SourcePos = center},damage);
            }
        }
        private bool IsValidAreaDamageTarget(Entity entity)
        {
            if (entity == null || !entity.Alive) return false;


            if (entity is EntityPlayer) return false;
            

            string code = entity.Code?.ToShortString() ?? "";
            if (string.IsNullOrWhiteSpace(code)) return false;
           

            string loweredCode = code.ToLowerInvariant();

            foreach (string part in confession.Config.AreaDamageTargetCodeContains)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
           
                if (loweredCode.Contains(part.ToLowerInvariant())) return true;
               
            }

            return false;
        }

        private void ProcessChargeGain(List<AnsweredDevotion> answeredDevotions)
        {
            if (answeredDevotions.Count == 0) return;
         

            bool changed = false;

            foreach (IGrouping<string, AnsweredDevotion> group in answeredDevotions.GroupBy(entry => entry.Community.CommunityId))
            {
                CommunityRecord community = group.First().Community;

                List<IServerPlayer> contributingPlayers = group.Select(entry => entry.Player).Where(CanPlayerContributeChargeToday).ToList();

                if (contributingPlayers.Count == 0) continue;
            

                int gain = CalculateChargeGain(contributingPlayers.Count);

                if (gain <= 0) continue;
               

                int oldCharge = community.Charge;

                community.MaxCharge = Math.Max(1, community.MaxCharge);
                community.Charge = Math.Min(community.MaxCharge, community.Charge + gain);

                if (community.Charge != oldCharge)
                {
                    confession.SyncLoadedCommunityCenter(community);
                    changed = true;
                }

                foreach (IServerPlayer player in contributingPlayers)
                {
                    IncrementPlayerDevotionContribution(player, player.PlayerUID);
                }
            }

            if (changed)
            {
                confession.Save();
            }
        }

        private IServerPlayer GetOnlineServerPlayer(string playerUid)
        {
            foreach (IPlayer onlinePlayer in sapi.World.AllOnlinePlayers)
            {
                if (onlinePlayer.PlayerUID == playerUid && onlinePlayer is IServerPlayer serverPlayer)
                {
                    return serverPlayer;
                }
            }

            return null;
        }

        private CommunityRecord FindValidCommunityInRange(IServerPlayer player)
        {
            Vec3d playerPos = player.Entity.Pos.XYZ;
            double influenceRadius = Math.Max(1, confession.Config.InfluenceRadius);
            double maxDistanceSq = influenceRadius * influenceRadius;

            CommunityRecord bestRecord = null;
            double bestDistanceSq = double.MaxValue;

            foreach (CommunityRecord record in confession.SaveData.CommunitiesById.Values)
            {
                BlockPos communityPos = new(record.X, record.Y, record.Z);
                Vec3d center = communityPos.ToVec3d().Add(0.5, 0.5, 0.5);

                double distanceSq = playerPos.SquareDistanceTo(center);

                if (distanceSq > maxDistanceSq) continue;

                if (!IsCommunityRecordValid(record)) continue;
             

                if (distanceSq < bestDistanceSq)
                {
                    bestRecord = record;
                    bestDistanceSq = distanceSq;
                }
            }

            return bestRecord;
        }
        private float GetTierValue(float[] valuesByTier, int tier, float fallback)
        {
            if (valuesByTier == null || valuesByTier.Length == 0)
            {
                return fallback;
            }

            int index = Math.Clamp(tier - 1, 0, valuesByTier.Length - 1);

            return valuesByTier[index];
        }

        private int GetChargeTier(CommunityRecord community)
        {
            if (community == null || community.Charge <= 0) return 0;
           
            int startCharge = Math.Max(1, confession.Config.StartCharge);
            int maxCharge = Math.Max(startCharge + 4, confession.Config.MaxCharge);

            int range = Math.Max(1, (maxCharge - startCharge) / 4);

            int tier2 = startCharge + range;
            int tier3 = startCharge + range * 2;
            int tier4 = startCharge + range * 3;

            if (community.Charge >= tier4) return 4;
            if (community.Charge >= tier3) return 3;
            if (community.Charge >= tier2) return 2;

            return 1;
        }

        private bool CanPlayerContributeChargeToday(IServerPlayer player)
        {
            PlayerDevotionStats stats = GetOrCreateDevotionStats(player.PlayerUID);
            int limit = Math.Max(0, confession.Config.EffectiveDevotionPulsesPerDay);

            return stats.ChargeContributingPulsesToday < limit;
        }

        private void IncrementPlayerDevotionContribution(IServerPlayer player, string playerUid)
        {
            PlayerDevotionStats stats = GetOrCreateDevotionStats(playerUid);
            stats.ChargeContributingPulsesToday++;
            if (stats.ChargeContributingPulsesToday >= confession.Config.EffectiveDevotionPulsesPerDay)
            {
                //player.SendMessage(GlobalConstants.InfoLogChatGroup, "[Confession]: You feel exhausted.", EnumChatType.Notification);
            }
        }

        private PlayerDevotionStats GetOrCreateDevotionStats(string playerUid)
        {
            int currentDay = (int)Math.Floor(sapi.World.Calendar.TotalDays);

            if (!confession.SaveData.DevotionStatsByPlayerUid.TryGetValue(playerUid, out PlayerDevotionStats stats))
            {
                stats = new PlayerDevotionStats{PlayerUid = playerUid,DayIndex = currentDay,ChargeContributingPulsesToday = 0};

                confession.SaveData.DevotionStatsByPlayerUid[playerUid] = stats;

                return stats;
            }

            if (stats.DayIndex != currentDay)
            {
                stats.DayIndex = currentDay;
                stats.ChargeContributingPulsesToday = 0;
            }

            return stats;
        }

        private int CalculateChargeGain(int contributingPlayerCount)
        {
            if (contributingPlayerCount <= 0) return 0;
      

            float multiplier = 1f + 2f * (1f - MathF.Exp(-0.45f * (contributingPlayerCount - 1)));

            float rawGain = confession.Config.BaseChargeGain * multiplier;

            return Math.Max(1, (int)MathF.Round(rawGain));
        }
        private bool IsCommunityRecordValid(CommunityRecord record)
        {
            if (record == null) return false;

            if (string.IsNullOrEmpty(record.CommunityId) || string.IsNullOrEmpty(record.BeliefCode)) return false;


            if (!confession.SaveData.BeliefsByCode.ContainsKey(record.BeliefCode)) return false;
          

            BlockPos pos = new(record.X, record.Y, record.Z);
            Block block = sapi.World.BlockAccessor.GetBlock(pos);

            if (block == null || !confession.IsCommunityCenterBlock(block)) return false;
         

            if (block.Variant?["state"] != "bound") return false;
          

            BlockEntityCommunityCenter be = sapi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCommunityCenter;

            if (be == null) return false;


            if (!be.IsBound) return false;


            if (be.CommunityId != record.CommunityId) return false;
          

            return true;
        }

        private void HandleUnansweredDevotion(IServerPlayer player, DevotionSession session)
        {
            RemoveDevotionHungerModifier(player);

            session.DevotionIsAnswered = false;
            session.CurrentCommunityId = "";
        }

        private void HandleAnsweredDevotion(IServerPlayer player, DevotionSession session, CommunityRecord community)
        {
            session.DevotionIsAnswered = true;
            session.CurrentCommunityId = community.CommunityId;
        }
        private bool HasPlayerMovedTooFar(IServerPlayer player, DevotionSession session)
        {
            Vec3d currentPos = player.Entity.Pos.XYZ;

            double dx = currentPos.X - session.StartPosition.X;
            double dy = currentPos.Y - session.StartPosition.Y;
            double dz = currentPos.Z - session.StartPosition.Z;

            return dx * dx + dy * dy + dz * dz > 0.0225;
        }
        private void OnPlayerDisconnect(IServerPlayer player)
        {
            StopDevotion(player);

        }

        private void ApplyPersonalDevotionRewards(IServerPlayer player, CommunityRecord community)
        {
            int tier = GetChargeTier(community);

            if (tier <= 0)
            {
                RemoveDevotionHungerModifier(player);
                return;
            }

            float temporalStabilityGain = GetTierValue(confession.Config.TemporalStabilityGainByTier,tier,0f);

            float hungerReduction = GetTierValue(confession.Config.HungerReductionByTier,tier,0f);

            float healingGain = GetTierValue(confession.Config.HealingGainByTier,tier,0f);

            ApplyTemporalStabilityReward(player, temporalStabilityGain);
            ApplyHealingReward(player, healingGain);
            ApplyDevotionHungerModifier(player, hungerReduction);
        }
        private void ApplyTemporalStabilityReward(IServerPlayer player, float gain)
        {
            double current = player.Entity.WatchedAttributes.GetDouble("temporalStability", 1);
            double next = Math.Min(1, current + gain);

            player.Entity.WatchedAttributes.SetDouble("temporalStability", next);
        }

        private void ApplyHealingReward(IServerPlayer player, float healingGain)
        {
            if (healingGain <= 0) return;

            player.Entity.ReceiveDamage(new DamageSource{Source = EnumDamageSource.Internal,Type = EnumDamageType.Heal},healingGain);
        }
        private void ApplyDevotionHungerModifier(IServerPlayer player, float hungerReduction)
        {
            if (player?.Entity == null) return;
            
            float reduction = GameMath.Clamp(hungerReduction, 0f, 0.95f);

            player.Entity.Stats.Set("hungerrate",DevotionHungerStatCode,-reduction,false);
        }

        private void RemoveDevotionHungerModifier(IServerPlayer player)
        {
            if (player?.Entity == null) return;
           
            player.Entity.Stats.Remove("hungerrate", DevotionHungerStatCode);
        }
    }
}