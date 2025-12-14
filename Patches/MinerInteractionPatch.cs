using Duckov.Bitcoins;
using HarmonyLib;
using JmcModLib.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoreBtcMiner.Patches
{
    [HarmonyPatch(typeof(BitcoinMinerView), "Show")]
    public static class MinerInteractionPatch
    {
        [HarmonyPrefix]
        public static void View_Show_Prefix()
        {
            Transform player = null;
            if (CharacterMainControl.Main != null) player = CharacterMainControl.Main.transform;

            // 如果拿不到角色，就没法用 Forward，直接返回
            if (player == null) return;

            BitcoinMiner targetMiner = null;
            Vector3 pPos = player.position;
            Vector3 pForward = player.forward; // 角色的正前方

            // ================================================================
            // 策略: 视锥检测 (距离 + 角度)
            // ================================================================

            // 1. 获取身边 3.0 米内的所有碰撞体
            Collider[] hits = Physics.OverlapSphere(pPos, 3.0f, -1, QueryTriggerInteraction.Collide);

            // 候选列表: (矿机, 距离, 夹角)
            var candidates = new List<(BitcoinMiner miner, float dist, float angle)>();

            foreach (var hit in hits)
            {
                var miner = hit.GetComponentInParent<BitcoinMiner>();
                if (miner == null || !miner.gameObject.activeInHierarchy) continue;

                // 计算物体中心点 (使用碰撞盒中心更准)
                Vector3 targetPos = hit.bounds.center;

                // 1. 计算距离 (忽略高度差，只算水平距离，体验更好)
                Vector3 dir = targetPos - pPos;
                dir.y = 0; // 扁平化向量，只看水平面
                float dist = dir.magnitude;

                // 2. 计算夹角 (0度代表正对，180度代表背对)
                // Vector3.Angle 返回两个向量的夹角
                float angle = Vector3.Angle(pForward, dir);

                candidates.Add((miner, dist, angle));
            }

            if (candidates.Count > 0)
            {
                // ============================================================
                // 评分排序逻辑 (核心)
                // ============================================================
                // 规则：
                // 1. 优先选"面前"的 (夹角 < 60度)()
                // 2. 在面前的里面，选距离最近的
                // 3. 如果都在背后，选距离最近的

                var sorted = candidates.OrderBy(x =>
                {
                    // 权重计算：
                    // 如果在面前 (angle < 60)，给予极大的排序优势 (-1000)
                    // 否则按距离排序
                    float score = x.dist;
                    if (x.angle < 60.0f)
                    {
                        score -= 1000.0f;
                    }
                    return score;
                }).ToList();


                if (ModLogger.GetLogLevel() <= LogLevel.Trace)
                {
                    ModLogger.Trace($"角色坐标: {pPos}");
                    ModLogger.Trace($"角色朝向: {pForward}");
                    foreach (var (miner, dist, angle) in sorted)
                    {
                        string status = angle < 60 ? "[面前]" : "[背后]";
                        ModLogger.Trace($"{status} {MinerManager.GetUniqueKey(miner)} | Dist: {dist:F2} | Angle: {angle:F1}°");
                    }
                }

                targetMiner = sorted[0].miner;
            }

            // ================================================================
            // 执行切换
            // ================================================================
            if (targetMiner != null)
            {
                if (!MinerManager.Miners.Contains(targetMiner)) MinerManager.Miners.Add(targetMiner);
                MinerManager.SwitchContext(targetMiner);
                ModLogger.Debug($"切换到 {MinerManager.GetUniqueKey(targetMiner)}");
            }
        }
    }
}