using Duckov.Buildings;
using HarmonyLib;
using MoreBtcMiner.Core;
using System;
using JmcModLib.Utils;
// 不需要 JmcModLib 的反射，直接用 Harmony 即可

namespace MoreBtcMiner
{
    [HarmonyPatch(typeof(BuildingManager), "GetBuildingInfo")]
    public static class SetBuildingLimitPatch
    {
        [HarmonyPostfix]
        public static void Postfix(string id, ref object __result)
        {
            if (__result == null) return;

            // 确认为矿机
            if (id == "MiningMachine")
            {
                // 1. 拆箱 (Unbox)
                var info = (BuildingInfo)__result;

                // 2. 修改上限
                // 读取配置文件的值
                info.maxAmount = ModSettings.MaxMinerCount;

                // 3. 装箱回写 (Box & Reassign)
                __result = info;

                // 调试日志 (可选)
                ModLogger.Trace($"Set MiningMachine limit to: {info.maxAmount}");
            }
        }
    }

    //// 拦截 BuildingInfo 的 ReachedAmountLimit 属性
    //[HarmonyPatch(typeof(BuildingInfo), "ReachedAmountLimit", MethodType.Getter)]
    //public static class BuildingLimitUnlocker
    //{
    //    [HarmonyPostfix]
    //    public static void Postfix(ref BuildingInfo __instance, ref bool __result)
    //    {
    //        // __instance 是当前正在被检查的建筑信息结构体
    //        // 我们检查它的 ID 是否为矿机
    //        if (__instance.id == "MiningMachine")
    //        {
    //            // 强制返回 false (表示未达到上限)
    //            // 这样 UI 就会认为还能造，BuyAndPlace 也会放行
    //            __result = false;
    //        }
    //    }
    //}
}