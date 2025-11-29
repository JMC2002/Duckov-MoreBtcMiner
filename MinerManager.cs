using System.Collections.Generic;
using Duckov.Bitcoins;
using JmcModLib.Reflection;
using JmcModLib.Utils;
using UnityEngine;

namespace MoreBtcMiner
{
    public static class MinerManager
    {
        public static List<BitcoinMiner> Miners = new List<BitcoinMiner>();

        // 缓存 SaveData 的类型，因为它是 private nested type，只需获取一次
        public static readonly System.Type Type_SaveData = typeof(BitcoinMiner).GetNestedType("SaveData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static string GetUniqueKey(BitcoinMiner miner)
        {
            var pos = miner.transform.position;
            return $"BitcoinMiner_{Mathf.RoundToInt(pos.x)}_{Mathf.RoundToInt(pos.y)}_{Mathf.RoundToInt(pos.z)}";
        }

        // 新增：用于存储从原版矿机捕捉到的数值
        // 初始为 null，表示还没捕捉到
        public static double? StandardWorkPerCoin = null;

        public static void SwitchContext(BitcoinMiner target)
        {
            if (target == null) return;

            // 直接使用 JmcModLib 设置 Instance
            var accInstance = MemberAccessor.Get(typeof(BitcoinMiner), "Instance");
            var current = accInstance.GetValue(null) as BitcoinMiner;

            if (current != target)
            {
                accInstance.SetValue(null, target);
                // ModLogger.Trace($"Context switched to miner ID: {target.GetInstanceID()}");
            }
        }
    }
}