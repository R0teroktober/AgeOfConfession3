using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AgeOfConfession
{

    [HarmonyPatch]
    public static class EntityPlayer_UpdateEyeHeight_DevotionPatch
    {
        private static readonly MethodInfo TargetMethodInfo = AccessTools.Method(typeof(EntityPlayer), "updateEyeHeight", new[] { typeof(float) });

        private static readonly MethodInfo GetPropertiesMethod = AccessTools.PropertyGetter(typeof(Entity), nameof(Entity.Properties));

        private static readonly FieldInfo EyeHeightField = AccessTools.Field(typeof(EntityProperties), nameof(EntityProperties.EyeHeight));

        private static readonly MethodInfo GetDevotionEyeHeightMethod = AccessTools.Method(typeof(EntityPlayer_UpdateEyeHeight_DevotionPatch), nameof(GetDevotionEyeHeight));

        static MethodBase TargetMethod()
        {
            return TargetMethodInfo;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions)
                .MatchStartForward(
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(ci =>(ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && Equals(ci.operand, GetPropertiesMethod)),
                    new CodeMatch(OpCodes.Ldfld, EyeHeightField)
                );

            if (!matcher.IsValid)
            {
                throw new InvalidOperationException("[Confession]: Could not find Properties getter + EyeHeight field load in EntityPlayer.updateEyeHeight()");
            }

            matcher
                .RemoveInstructions(3)
                .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, GetDevotionEyeHeightMethod)
                );

            return matcher.InstructionEnumeration();
        }

        private static double GetDevotionEyeHeight(EntityPlayer player)
        {
            double vanillaEyeHeight = player.Properties.EyeHeight;

            if (!DevotionClientState.AppliesTo(player))
            {
                return vanillaEyeHeight;
            }

            return vanillaEyeHeight * 0.63;
        }
    }
}