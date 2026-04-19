using AgeOfConfession;
using System;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AgeOfConfession
{
    public class AgeOfConfessionModSystem : ModSystem
    {
        public static ConfessionConfig Config { get; private set; }
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
           
            Config = api.LoadModConfig<ConfessionConfig>("confession-config.json");
            if (Config == null)
            {
                Config = new ConfessionConfig()
                {

       
                };

                api.StoreModConfig(Config, "ageofconfession.json");
                api.Logger.Notification("[AgeOfConfession] Default config created.");
            }
            else
            {
                api.Logger.Notification("[AgeOfConfession] Config loaded successfully.");
            }
        }
    }

}
