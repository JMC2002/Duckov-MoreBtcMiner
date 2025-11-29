using Duckov.Bitcoins;
using HarmonyLib;
using ItemStatsSystem.Data;
using JmcModLib.Reflection;
using JmcModLib.Utils;
using Saves;
using System;

namespace MoreBtcMiner.Patches
{
    [HarmonyPatch(typeof(BitcoinMiner))]
    public static class MinerLifecyclePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("Awake")]
        public static bool Awake_Prefix(BitcoinMiner __instance)
        {
            // 1. 清理无效引用并注册
            MinerManager.Miners.RemoveAll(m => m == null);
            MinerManager.Miners.Add(__instance);

            // 2. 数值平衡同步 (自动捕捉原版值并应用到新矿机)
            var accWork = MemberAccessor.Get(typeof(BitcoinMiner), "workPerCoin");
            double currentVal = (double)accWork.GetValue(__instance);

            // 捕捉
            if (MinerManager.StandardWorkPerCoin == null && Math.Abs(currentVal - 1.0) > 0.001)
                MinerManager.StandardWorkPerCoin = currentVal;
            // 应用
            if (MinerManager.StandardWorkPerCoin != null && Math.Abs(currentVal - 1.0) < 0.001)
                accWork.SetValue(__instance, MinerManager.StandardWorkPerCoin.Value);

            // 3. 单例占位 (防止原版报错)
            var accInstance = MemberAccessor.Get(typeof(BitcoinMiner), "Instance");
            if (accInstance.GetValue(null) == null) accInstance.SetValue(null, __instance);

            // 4. 绑定保存事件
            var methodSave = typeof(BitcoinMiner).GetMethod("Save", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (methodSave != null)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), __instance, methodSave);
                var eventInfo = typeof(SavesSystem).GetEvent("OnCollectSaveData");
                eventInfo.AddEventHandler(null, del);
            }


            ModLogger.Debug($"矿机 Awake 注册完毕: {MinerManager.GetUniqueKey(__instance)}, 当前有: {MinerManager.Miners.Count} 个矿机");
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnDestroy")]
        public static bool OnDestroy_Prefix(BitcoinMiner __instance)
        {
            ModLogger.Debug($"矿机 OnDestroy 清理: {MinerManager.GetUniqueKey(__instance)}");
            MinerManager.Miners.Remove(__instance);

            // 仅清理事件，不清除存档数据 (实现原址重建找回功能)
            var methodSave = typeof(BitcoinMiner).GetMethod("Save", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (methodSave != null)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), __instance, methodSave);
                var eventInfo = typeof(SavesSystem).GetEvent("OnCollectSaveData");
                eventInfo.RemoveEventHandler(null, del);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Save")]
        public static bool Save_Prefix(BitcoinMiner __instance)
        {
            Type tMiner = typeof(BitcoinMiner);
            Type tSaveData = MinerManager.Type_SaveData;

            // 状态检查
            bool loading = (bool)MemberAccessor.Get(tMiner, "Loading").GetValue(__instance);
            bool init = (bool)MemberAccessor.Get(tMiner, "Initialized").GetValue(__instance);
            if (loading || !init) return false;

            try
            {
                // 构造 SaveData
                object dataStruct = Activator.CreateInstance(tSaveData);

                // 填充数据
                var item = MemberAccessor.Get(tMiner, "item").GetValue(__instance) as ItemStatsSystem.Item;
                var work = MemberAccessor.Get(tMiner, "work").GetValue(__instance);
                var time = MemberAccessor.Get(tMiner, "lastUpdateDateTimeRaw").GetValue(__instance);
                var perf = MemberAccessor.Get(tMiner, "cachedPerformance").GetValue(__instance);

                MemberAccessor.Get(tSaveData, "itemData").SetValue(dataStruct, ItemTreeData.FromItem(item));
                MemberAccessor.Get(tSaveData, "work").SetValue(dataStruct, work);
                MemberAccessor.Get(tSaveData, "lastUpdateDateTimeRaw").SetValue(dataStruct, time);
                MemberAccessor.Get(tSaveData, "cachedPerformance").SetValue(dataStruct, perf);

                // 保存到 UniqueKey
                string uniqueKey = MinerManager.GetUniqueKey(__instance);

                // Save<T>(string key, object val) 使用 typeof(object) 占位泛型参数
                var saveMethod = MethodAccessor.Get(typeof(SavesSystem), "Save", new Type[] { typeof(string), typeof(object) });
                saveMethod.MakeGeneric(tSaveData).Invoke(null, uniqueKey, dataStruct);

                ModLogger.Debug($"矿机 {uniqueKey} 已保存");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"矿机保存失败", ex);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Load")]
        public static bool Load_Prefix(BitcoinMiner __instance)
        {
            Type tMiner = typeof(BitcoinMiner);
            Type tSaveData = MinerManager.Type_SaveData;
            string uniqueKey = MinerManager.GetUniqueKey(__instance);

            ModLogger.Debug($"矿机 {uniqueKey} 尝试加载存档");

            if (SavesSystem.KeyExisits(uniqueKey))
            {
                try
                {
                    ModLogger.Debug($"矿机 {uniqueKey} 存档存在，尝试加载");
                    var loadMethod = MethodAccessor.Get(typeof(SavesSystem), "Load", new Type[] { typeof(string) });
                    object dataStruct = loadMethod.MakeGeneric(tSaveData).Invoke(null, uniqueKey);

                    // 数据安检：防止 Setup 崩溃
                    var accItemData = MemberAccessor.Get(tSaveData, "itemData");
                    if (accItemData.GetValue(dataStruct) == null)
                    {
                        ModLogger.Debug($"存档数据缺失 itemData 字段，重置矿机 {uniqueKey}");
                        MethodAccessor.Get(tMiner, "Initialize").Invoke(__instance);
                    }
                    else
                    {
                        MethodAccessor.Get(tMiner, "Setup", new[] { tSaveData }).Invoke(__instance, dataStruct);
                        ModLogger.Debug($"矿机 {uniqueKey} 存档加载成功");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[Load] Error {uniqueKey}, resetting.", ex);
                    MethodAccessor.Get(tMiner, "Initialize").Invoke(__instance);
                }
            }
            else
            {
                ModLogger.Debug($"矿机 {uniqueKey} 无存档，执行初始化");
                MethodAccessor.Get(tMiner, "Initialize").Invoke(__instance);
            }

            return false;
        }
    }
}