using Duckov.Bitcoins;
using Duckov.Buildings;
using JmcModLib.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreBtcMiner
{
    public static class MinerManager
    {
        // 活跃矿机列表
        public static List<BitcoinMiner> Miners = [];

        // 缓存私有结构体类型
        public static readonly Type Type_SaveData = typeof(BitcoinMiner).GetNestedType("SaveData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // 缓存原版数值
        public static double? StandardWorkPerCoin = null;

        /// <summary>
        /// 获取绝对稳定的 Key (网格坐标优先)
        /// </summary>
        public static string GetUniqueKey(BitcoinMiner miner)
        {
            try
            {
                // 1. 尝试获取 Building 组件
                var building = miner.GetComponent<Building>() ?? miner.GetComponentInParent<Building>();

                if (building != null)
                {
                    var accData = MemberAccessor.Get(typeof(Building), "data");
                    var dataObj = accData.GetValue(building);

                    if (dataObj != null)
                    {
                        var accCoord = MemberAccessor.Get(dataObj.GetType(), "Coord");
                        var coord = (Vector2Int)accCoord.GetValue(dataObj);

                        // 生成 Key: BitcoinMiner_Grid_10_5
                        return $"BitcoinMiner_Grid_{coord.x}_{coord.y}";
                    }
                }
            }
            catch (Exception)
            {
                // 
                // ModLogger.Warn($"Failed to get Grid Key: {ex.Message}");
            }

            // ===============================================================
            // 静态矿机 / 异常情况 -> 使用模糊浮点坐标
            // ===============================================================
            // 场景自带的矿机没有 Building 组件，或者是数据读取失败
            // 使用 F1 (保留1位小数) 忽略微小的物理漂移
            var pos = miner.transform.position;
            return $"BitcoinMiner_Pos_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
        }

        /// <summary>
        /// 切换上下文：强制修改单例引用
        /// </summary>
        public static void SwitchContext(BitcoinMiner target)
        {
            if (target == null) return;
            var acc = MemberAccessor.Get(typeof(BitcoinMiner), "Instance");
            if (acc.GetValue(null) != target)
            {
                acc.SetValue(null, target);
            }
        }
    }
}