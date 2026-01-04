using Duckov.Bitcoins;
using HarmonyLib;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using JmcModLib.Reflection;
using JmcModLib.Utils;
using System;
using System.Collections;
using System.Reflection;

namespace MoreBtcMiner.Patches
{
    [HarmonyPatch(typeof(MiningMachineVisual), "Update")]
    public static class MinerVisualPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MiningMachineVisual __instance)
        {
            // 检查初始化状态
            var accInited = MemberAccessor.Get(typeof(MiningMachineVisual), "inited");
            bool inited = accInited.GetValue<MiningMachineVisual, bool>(__instance);

            if (inited) return false;

            // 获取本地组件
            var localMiner = __instance.GetComponentInParent<BitcoinMiner>();
            if (localMiner == null)
            {
                ModLogger.Trace("不存在localMiner");   // 此处建造时未放置的时候可能走这个打印
                return false;
            }

            var accItem = MemberAccessor.Get(typeof(BitcoinMiner), "Item");
            var itemObj = accItem.GetValue<BitcoinMiner, Item>(localMiner);

            if (itemObj != null)
            {
                // 标记已初始化
                accInited.SetValue(__instance, true);

                // 注入字段
                MemberAccessor.Get(typeof(MiningMachineVisual), "minnerItem").SetValue(__instance, itemObj);
                var slots = itemObj.Slots;
                MemberAccessor.Get(typeof(MiningMachineVisual), "slots").SetValue(__instance, slots);

                // =========================================================
                // 核心修复: 手动执行一次视觉刷新 (Manual Refresh)
                // =========================================================
                try
                {
                    // 获取显示对象列表 List<MiningMachineCardDisplay>
                    var accCards = MemberAccessor.Get(typeof(MiningMachineVisual), "cardsDisplay");
                    var cardsList = accCards.GetValue<MiningMachineVisual, IList>(__instance);

                    if (cardsList != null && slots != null)
                    {
                        // 遍历插槽
                        for (int i = 0; i < slots.Count && i < cardsList.Count; i++)
                        {
                            object displayObj = cardsList[i];
                            if (displayObj == null) continue;

                            // 获取插槽内容
                            var slot = slots[i];
                            var content = slot.Content;

                            // 默认类型: 0 (Normal)
                            int cardTypeValue = 0;

                            // 如果插槽有显卡，检查显卡类型
                            if (content != null)
                            {
                                // 尝试获取 ItemSetting_GPU 组件
                                var gpuSetting = content.GetComponent("ItemSetting_GPU");
                                if (gpuSetting != null)
                                {
                                    // 反射获取 cardType 字段 (enum)
                                    var fCardType = gpuSetting.GetType().GetField("cardType");
                                    if (fCardType != null)
                                    {
                                        cardTypeValue = (int)fCardType.GetValue(gpuSetting);
                                    }
                                }
                            }

                            // 调用 MiningMachineCardDisplay.SetVisualActive
                            // 方法签名: void SetVisualActive(bool active, CardTypes cardType)
                            // CardTypes 是枚举，可以直接传 int
                            var methodSetVisual = displayObj.GetType().GetMethod("SetVisualActive");
                            // 参数: [是否有内容, 显卡类型]
                            methodSetVisual?.Invoke(displayObj, [content != null, cardTypeValue]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[Visual] Manual refresh failed: {ex.Message}");
                }

                // =========================================================
                // 绑定事件 (用于后续更新)
                // =========================================================
                try
                {
                    var methodOnChanged = typeof(MiningMachineVisual).GetMethod(
                        "OnSlotContentChanged",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );

                    if (methodOnChanged != null)
                    {
                        var actionType = typeof(Action<Item, Slot>);
                        var handler = Delegate.CreateDelegate(actionType, __instance, methodOnChanged);
                        var eventInfo = typeof(Item).GetEvent("onSlotContentChanged");
                        eventInfo?.AddEventHandler(itemObj, handler);
                    }
                    else
                    {
                        ModLogger.Warn("[Visual] Could not find OnSlotContentChanged method. Dynamic updates might fail.");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[Visual] Event binding failed: {ex.Message}");
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(MiningMachineVisual), "OnDestroy")]
    public static class MinerVisualDestroyPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MiningMachineVisual __instance)
        {
            var accMinnerItem = MemberAccessor.Get(typeof(MiningMachineVisual), "minnerItem");
            var itemObj = accMinnerItem.GetValue(__instance) as Item;

            if (itemObj != null)
            {
                try
                {
                    var methodOnChanged = typeof(MiningMachineVisual).GetMethod(
                        "OnSlotContentChanged",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );

                    if (methodOnChanged != null)
                    {
                        var actionType = typeof(Action<Item, Slot>);
                        var handler = Delegate.CreateDelegate(actionType, __instance, methodOnChanged);
                        var eventInfo = typeof(Item).GetEvent("onSlotContentChanged");
                        eventInfo?.RemoveEventHandler(itemObj, handler);
                    }
                }
                catch { }
            }
            return false;
        }
    }
}