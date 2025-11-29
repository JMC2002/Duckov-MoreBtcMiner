using Duckov.Bitcoins;
using Duckov.UI;
using HarmonyLib;
using ItemStatsSystem.Data;
using JmcModLib.Reflection;
using JmcModLib.Utils;
using Saves;
using System;
using UnityEngine;

namespace MoreBtcMiner
{
    using Duckov.Bitcoins;
    using Duckov.Buildings;
    using HarmonyLib;
    using JmcModLib.Reflection;
    using JmcModLib.Utils;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    namespace MoreBtcMiner
    {
        // =============================================================
        // 补丁：给新生成的矿机建筑外壳“注入灵魂”
        // =============================================================
        [HarmonyPatch(typeof(Building), "Setup")]
        public static class InjectMinerScriptPatch
        {
            [HarmonyPostfix]
            public static void Postfix(Building __instance, BuildingManager.BuildingData data)
            {
                // 1. 检查建筑 ID (确认为 "MiningMachine")
                if (data.ID == "MiningMachine")
                {
                    // ModLogger.Trace($"[Inject] Setup called for MiningMachine (GUID: {data.GUID})");

                    // 2. 检查是否已有 BitcoinMiner 脚本
                    var minerScript = __instance.GetComponent<BitcoinMiner>();

                    if (minerScript == null)
                    {
                        // ModLogger.Info($"[Inject] Attaching BitcoinMiner script to new instance (GUID: {data.GUID})");

                        // 3. 动态添加脚本
                        minerScript = __instance.gameObject.AddComponent<BitcoinMiner>();

                        // 注意：AddComponent 会立即调用 Awake。
                        // 我们的 Awake_Prefix 补丁应该已经生效，把它加入了 MinerManager。

                        // 4. 检查是否是新建造的（没有存档数据）
                        // 我们可以简单地检查它是否初始化了。
                        // 此时 Awake 已过，Instance 应该被设置（或占位），MinerManager 应该已有记录。

                        // 尝试手动调用 Initialize，因为新挂的脚本没有存档数据可读
                        // 只有当它是“新建造”的时候才需要 Initialize。
                        // 如果是从存档加载的，稍后 BitcoinMiner.Load 会被调用（如果它注册了事件）。

                        // 这里的难点是：如果是从存档加载的 Building，稍后 SaveSystem 会调用 Load。
                        // 如果是刚造出来的，我们需要手动 Initialize。

                        // 我们可以检查 SavesSystem 是否有这个 Key。但 Key 是基于位置的。
                        // 这里的 GameObject 位置可能还没更新？
                        // Building.Setup 里: transform.localRotation 刚被设置。
                        // 坐标由 functionContainer 的父级决定？

                        // 简单策略：总是尝试调用 Initialize。
                        // 如果后续 Load 覆盖了它，也没关系。

                        // 使用反射调用 private void Initialize()
                        var methodInit = MethodAccessor.Get(typeof(BitcoinMiner), "Initialize");

                        // 为了防止 Initialize 内部做复杂的异步操作出错，我们可以先检查一下状态
                        var accInit = MemberAccessor.Get(typeof(BitcoinMiner), "Initialized");
                        bool isInit = (bool)accInit.GetValue(minerScript);

                        if (!isInit)
                        {
                            // ModLogger.Trace("[Inject] Manually initializing new miner script...");
                            methodInit.Invoke(minerScript);
                        }
                    }
                }
            }
        }
    }



    [HarmonyPatch]
    public class BitcoinMinerPatches
    {
        // =======================================================
        // Awake: 注册矿机到管理器
        // =======================================================
        [HarmonyPatch(typeof(BitcoinMiner), "Awake")]
        [HarmonyPrefix]
        public static bool Awake_Prefix(BitcoinMiner __instance)
        {
            // 移除销毁的对象
            MinerManager.Miners.RemoveAll(m => m == null);
            MinerManager.Miners.Add(__instance);

            // 数值同步 (保留)
            var accWork = MemberAccessor.Get(typeof(BitcoinMiner), "workPerCoin");
            double currentVal = (double)accWork.GetValue(__instance);
            if (MinerManager.StandardWorkPerCoin == null && Math.Abs(currentVal - 1.0) > 0.001) MinerManager.StandardWorkPerCoin = currentVal;
            if (MinerManager.StandardWorkPerCoin != null && Math.Abs(currentVal - 1.0) < 0.001) accWork.SetValue(__instance, MinerManager.StandardWorkPerCoin.Value);

            // 单例占位 (保留)
            var accInstance = MemberAccessor.Get(typeof(BitcoinMiner), "Instance");
            if (accInstance.GetValue(null) == null) accInstance.SetValue(null, __instance);

            // 事件绑定 (保留)
            var methodSave = typeof(BitcoinMiner).GetMethod("Save", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (methodSave != null)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), __instance, methodSave);
                var eventInfo = typeof(SavesSystem).GetEvent("OnCollectSaveData");
                eventInfo.AddEventHandler(null, del);
            }

            return false;
        }

        [HarmonyPatch(typeof(BitcoinMiner), "OnDestroy")]
        [HarmonyPrefix]
        public static bool OnDestroy_Prefix(BitcoinMiner __instance)
        {
            // 1. 从管理器移除引用
            MinerManager.Miners.Remove(__instance);

            // 2. 清理保存事件监听 (防止内存泄漏)
            var methodSave = typeof(BitcoinMiner).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodSave != null)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), __instance, methodSave);
                var eventInfo = typeof(SavesSystem).GetEvent("OnCollectSaveData");
                eventInfo.RemoveEventHandler(null, del);
            }

            // 3. 这里的关键是：
            // 我们不再尝试 SendToPlayer (避免报错)
            // 我们也不再 Save 空数据 (保留原存档)

            return false;
        }

        // =======================================================
        // Save: 使用参数数组精确查找重载
        // =======================================================
        [HarmonyPatch(typeof(BitcoinMiner), "Save")]
        [HarmonyPrefix]
        public static bool Save_Prefix(BitcoinMiner __instance)
        {
            Type tMiner = typeof(BitcoinMiner);
            Type tSaveData = MinerManager.Type_SaveData;

            bool loading = (bool)MemberAccessor.Get(tMiner, "Loading").GetValue<BitcoinMiner, bool>(__instance);
            bool init = (bool)MemberAccessor.Get(tMiner, "Initialized").GetValue<BitcoinMiner, bool>(__instance);

            if (loading || !init) return false;

            try
            {
                // 构造 SaveData
                object dataStruct = Activator.CreateInstance(tSaveData);

                var item = MemberAccessor.Get(tMiner, "item").GetValue(__instance) as ItemStatsSystem.Item;
                var work = MemberAccessor.Get(tMiner, "work").GetValue(__instance);
                var time = MemberAccessor.Get(tMiner, "lastUpdateDateTimeRaw").GetValue(__instance);
                var perf = MemberAccessor.Get(tMiner, "cachedPerformance").GetValue(__instance);

                MemberAccessor.Get(tSaveData, "itemData").SetValue(dataStruct, ItemTreeData.FromItem(item));
                MemberAccessor.Get(tSaveData, "work").SetValue(dataStruct, work);
                MemberAccessor.Get(tSaveData, "lastUpdateDateTimeRaw").SetValue(dataStruct, time);
                MemberAccessor.Get(tSaveData, "cachedPerformance").SetValue(dataStruct, perf);

                string uniqueKey = MinerManager.GetUniqueKey(__instance);

                // --- 修复点 ---
                // Save<T>(string key, T value)
                // 参数1: string
                // 参数2: T (泛型位)，传入 null 以跳过类型检测，匹配正确重载
                var saveMethod = MethodAccessor.Get(typeof(SavesSystem), "Save", new Type[] { typeof(string), typeof(object) });

                // 闭合泛型 -> Invoke(null, args)
                saveMethod.MakeGeneric(tSaveData).Invoke(null, uniqueKey, dataStruct);
            }
            catch (Exception ex)
            {
                ModLogger.Error("BitcoinMiner Save Failed", ex);
            }

            return false;
        }

        // =======================================================
        // Load: 使用参数数组精确查找重载
        // =======================================================
        [HarmonyPatch(typeof(BitcoinMiner), "Load")]
        [HarmonyPrefix]
        public static bool Load_Prefix(BitcoinMiner __instance)
        {
            Type tMiner = typeof(BitcoinMiner);
            Type tSaveData = MinerManager.Type_SaveData;
            string uniqueKey = MinerManager.GetUniqueKey(__instance);

            // 1. 尝试读取该位置的存档
            if (SavesSystem.KeyExisits(uniqueKey))
            {
                try
                {
                    // 读取数据
                    var loadMethod = MethodAccessor.Get(typeof(SavesSystem), "Load", new Type[] { typeof(string) });
                    object dataStruct = loadMethod.MakeGeneric(tSaveData).Invoke(null, uniqueKey);

                    // 这里的安检依然保留，防止万一读到坏档
                    var accItemData = MemberAccessor.Get(tSaveData, "itemData");
                    object valItemData = accItemData.GetValue(dataStruct);

                    if (valItemData == null)
                    {
                        // 如果读出来是坏的，就重置
                        var initMethod = MethodAccessor.Get(tMiner, "Initialize");
                        initMethod.Invoke(__instance);
                    }
                    else
                    {
                        // 数据正常，加载旧显卡
                        var setupMethod = MethodAccessor.Get(tMiner, "Setup", new[] { tSaveData });
                        setupMethod.Invoke(__instance, dataStruct);
                        // ModLogger.Info($"[Load] Restored miner data at {uniqueKey}");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[Load] Error loading {uniqueKey}, resetting.", ex);
                    var initMethod = MethodAccessor.Get(tMiner, "Initialize");
                    initMethod.Invoke(__instance);
                }
            }
            else
            {
                // 2. 没有存档 -> 新矿机
                var initMethod = MethodAccessor.Get(tMiner, "Initialize");
                initMethod.Invoke(__instance);
            }

            return false;
        }

        [HarmonyPatch(typeof(BitcoinMinerView), "Show")]
        [HarmonyPrefix]
        public static void View_Show_Prefix()
        {
            // 确保角色或相机存在
            Transform playerTransform = null;
            Camera mainCam = Camera.main;

            if (CharacterMainControl.Main != null)
            {
                playerTransform = CharacterMainControl.Main.transform;
            }
            else if (mainCam != null)
            {
                playerTransform = mainCam.transform;
            }

            if (playerTransform == null) return;

            BitcoinMiner targetMiner = null;

            // ================================================================
            // 策略 A (精准): 射线检测 (Raycast) - 玩家在看谁？
            // ================================================================
            if (mainCam != null)
            {
                // 从屏幕中心发射射线 (通常准星都在屏幕中心)
                Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                RaycastHit hit;

                // 检测距离设为 5米 (稍微大于一般的交互距离即可)
                // LayerMask 使用 Default 层或者交互层，这里暂不指定，检测所有物体
                if (Physics.Raycast(ray, out hit, 6.0f))
                {
                    // 尝试从碰撞到的物体或其父级获取 BitcoinMiner 组件
                    var miner = hit.collider.GetComponentInParent<BitcoinMiner>();
                    if (miner != null)
                    {
                        targetMiner = miner;
                        // ModLogger.Trace($"[View_Show] Raycast hit miner: {miner.GetInstanceID()}");
                    }
                }
            }

            // ================================================================
            // 策略 B (保底): 距离检测 - 谁离我最近？
            // (只有当射线没打中任何矿机时才执行，比如隔着玻璃或者碰撞体异常)
            // ================================================================
            if (targetMiner == null)
            {
                // ModLogger.Trace("[View_Show] Raycast missed, falling back to distance check...");

                float minDst = 5.0f; // 交互距离
                foreach (var miner in MinerManager.Miners)
                {
                    if (miner == null || !miner.gameObject.activeInHierarchy) continue;

                    float d = Vector3.Distance(playerTransform.position, miner.transform.position);
                    if (d < minDst)
                    {
                        minDst = d;
                        targetMiner = miner;
                    }
                }
            }

            // ================================================================
            // 执行切换
            // ================================================================
            if (targetMiner != null)
            {
                // 如果这个矿机还没注册 (以防万一)，注册它
                if (!MinerManager.Miners.Contains(targetMiner))
                {
                    MinerManager.Miners.Add(targetMiner);
                }

                MinerManager.SwitchContext(targetMiner);
            }
            else
            {
                // ModLogger.Warn("[View_Show] Could not determine target miner.");
            }
        }
    }
}