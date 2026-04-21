using System.Collections.Generic;

namespace AgeOfConfession
{
    public class ConfessionConfig
    {
        public int StartCharge { get; set; } = 540;
        public int MaxCharge { get; set; } = 1980;
        public int CommunityExclusionRadius { get; set; } = 100;
        public int InfluenceRadius { get; set; } = 10;

        public int BaseChargeGain { get; set; } = 3;
        public int DecayRate { get; set; } = 3;

        public int EmptyBeliefDeletionDays { get; set; } = 365;
        public int EffectiveDevotionPulsesPerDay { get; set; } = 8;

        public float[] TemporalStabilityGainByTier { get; set; } =
        {
        0.0075f, 0.015f, 0.025f, 0.04f
        };

        public float[] HungerReductionByTier { get; set; } =
        {
        0.10f, 0.25f, 0.40f, 0.50f
        };

        public float[] HealingGainByTier { get; set; } =
        {
        0.1f, 0.2f, 0.4f, 0.6f
        };

        public float[] AreaDamageByTier { get; set; } =
        {
        1f, 2f, 4f, 5f
        };

        public string[] AreaDamageTargetCodeContains { get; set; } =
        {
            "drifter",
            "bowtorn",
            "shiver"
        };

        public string[] BeliefFounderAllowedClasses { get; set; } =
        {
           "malefactor"
        };

    }
}