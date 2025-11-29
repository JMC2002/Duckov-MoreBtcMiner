using Duckov.Bitcoins;
using Duckov.Buildings;
using Duckov.Buildings.UI;
using HarmonyLib;
using JmcModLib.Config;
using JmcModLib.Config.UI;
using JmcModLib.Reflection;

namespace MoreBtcMiner.Patches
{
    public static class BuildingLogicPatch
    {
        [UIIntSlider(0, 10)]
        [Config("矿机建造上限(0 代表无上限)")]
        public static int MaxMinerCount = 2;

        // 补丁 A: 修复建造逻辑检查
        [HarmonyPatch(typeof(BuildingManager), "GetBuildingInfo")]
        public static class LogicLimit
        {
            [HarmonyPostfix]
            public static void Postfix(string id, ref object __result)
            {
                if (__result == null) return;
                if (id == "MiningMachine")
                {
                    var info = (BuildingInfo)__result;
                    info.maxAmount = MaxMinerCount;
                    __result = info;
                }
            }
        }

        // 修复 UI 显示限制
        [HarmonyPatch(typeof(BuildingBtnEntry), "Setup")]
        public static class UILimit
        {
            [HarmonyPrefix]
            public static void Prefix(ref BuildingInfo buildingInfo)
            {
                if (buildingInfo.id == "MiningMachine")
                {
                    buildingInfo.maxAmount = MaxMinerCount;
                }
            }
        }

        // 给新建筑注入脚本
        [HarmonyPatch(typeof(Building), "Setup")]
        public static class ScriptInjection
        {
            [HarmonyPostfix]
            public static void Postfix(Building __instance, BuildingManager.BuildingData data)
            {
                if (data.ID == "MiningMachine")
                {
                    var minerScript = __instance.GetComponent<BitcoinMiner>();
                    if (minerScript == null)
                    {
                        minerScript = __instance.gameObject.AddComponent<BitcoinMiner>();

                        // 检查是否需要手动初始化
                        var accInit = MemberAccessor.Get(typeof(BitcoinMiner), "Initialized");
                        if (!(bool)accInit.GetValue(minerScript))
                        {
                            MethodAccessor.Get(typeof(BitcoinMiner), "Initialize").Invoke(minerScript);
                        }
                    }
                }
            }
        }
    }
}