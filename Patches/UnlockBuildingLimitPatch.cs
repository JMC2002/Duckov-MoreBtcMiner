using Duckov.Buildings;
using Duckov.Buildings.UI; // 引用 UI 命名空间
using HarmonyLib;
using JmcModLib.Utils;
using MoreBtcMiner.Core;

namespace MoreBtcMiner
{
    // =============================================================
    // 补丁 A: 修复建造逻辑 (BuyAndPlace 调用这个)
    // =============================================================
    [HarmonyPatch(typeof(BuildingManager), "GetBuildingInfo")]
    public static class LogicLimitPatch
    {
        [HarmonyPostfix]
        public static void Postfix(string id, ref object __result)
        {
            if (__result == null) return;

            if (id == "MiningMachine")
            {
                // 拆箱修改再装箱
                var info = (BuildingInfo)__result;
                info.maxAmount = ModSettings.MaxMinerCount; // 使用配置值
                __result = info;
            }
        }
    }

    // =============================================================
    // 补丁 B: 修复 UI 显示 (按钮初始化调用这个)
    // =============================================================
    [HarmonyPatch(typeof(BuildingBtnEntry), "Setup")]
    public static class UILimitPatch
    {
        // 使用 Prefix 拦截参数，ref 允许修改传入的值
        [HarmonyPrefix]
        public static void Prefix(ref BuildingInfo buildingInfo)
        {
            // 检查是不是矿机
            if (buildingInfo.id == "MiningMachine")
            {
                // 直接修改传入 UI 的结构体参数
                buildingInfo.maxAmount = ModSettings.MaxMinerCount; // 使用配置值

                // ModLogger.Trace($"[UI] Fixed MiningMachine limit to {ModSettings.MaxMinerCount}");
            }
        }
    }
}