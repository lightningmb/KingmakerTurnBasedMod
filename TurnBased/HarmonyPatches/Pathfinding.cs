using Harmony12;
using Kingmaker.View;
using TurnBasedUpdated.Utility;
using static TurnBasedUpdated.Utility.SettingsWrapper;
using static TurnBasedUpdated.Utility.StatusWrapper;

namespace TurnBasedUpdated.HarmonyPatches
{
    static class Pathfinding
    {
        // moving through ... feature
        [HarmonyPatch(typeof(UnitMovementAgent), nameof(UnitMovementAgent.AvoidanceDisabled), MethodType.Getter)]
        static class UnitMovementAgent_AvoidanceDisabled_Patch
        {
            [HarmonyPostfix]
            static void Postfix(UnitMovementAgent __instance, ref bool __result)
            {
                if (IsInCombat() && !__result)
                {
                    __result = CurrentUnit().CanMoveThrough(__instance.Unit?.EntityData);
                }
            }
        }

        // forbid moving through non-allys
        [HarmonyPatch(typeof(UnitMovementAgent), "IsSoftObstacle", typeof(UnitMovementAgent))]
        static class UnitMovementAgent_IsSoftObstacle_Patch
        {
            [HarmonyPrefix]
            static bool Prefix(UnitMovementAgent __instance, ref bool __result)
            {
                if (IsInCombat() && DoNotMovingThroughNonAlly)
                {
                    __result = !__instance.CombatMode;
                    return false;
                }
                return true;
            }
        }

        // modify collision radius
        //[HarmonyPatch(typeof(UnitMovementAgent), nameof(UnitMovementAgent.Corpulence), MethodType.Getter)]
        //static class UnitMovementAgent_get_Corpulence_Patch
        //{
        //    [HarmonyPostfix]
        //    static void Postfix(ref float __result)
        //    {
        //        if (IsInCombat())
        //        {
        //            __result *= RadiusOfCollision;
        //        }
        //    }
        //}
    }
}