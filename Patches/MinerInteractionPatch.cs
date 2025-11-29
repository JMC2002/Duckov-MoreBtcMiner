using Duckov.Bitcoins;
using HarmonyLib;
using JmcModLib.Utils;
using UnityEngine;

namespace MoreBtcMiner.Patches
{
    [HarmonyPatch(typeof(BitcoinMinerView), "Show")]
    public static class MinerInteractionPatch
    {
        [HarmonyPrefix]
        public static void View_Show_Prefix()
        {
            Transform playerTransform = null;
            Camera mainCam = Camera.main;

            if (CharacterMainControl.Main != null) playerTransform = CharacterMainControl.Main.transform;
            else if (mainCam != null) playerTransform = mainCam.transform;

            if (playerTransform == null) return;

            BitcoinMiner targetMiner = null;

            //if (mainCam != null)
            //{
            //    // 从屏幕中心发射
            //    Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

            //    RaycastHit hit;
            //    // 参数说明: 
            //    // ray: 射线方向
            //    // radius: 0.4f (发射一个半径0.4米的粗圆柱体，更容易打中物体)
            //    // distance: 10.0f (检测距离)
            //    // layerMask: -1 (检测所有层)
            //    // queryTriggerInteraction: Collide (关键！强制检测 Trigger 类型的碰撞体)
            //    if (Physics.SphereCast(ray, 0.4f, out hit, 10.0f, -1, QueryTriggerInteraction.Collide))
            //    {
            //        // 尝试获取组件
            //        targetMiner = hit.collider.GetComponentInParent<BitcoinMiner>();

            //        // 如果没找到，可能是打到了子物体，尝试向上找
            //        if (targetMiner == null && hit.transform.parent != null)
            //        {
            //            targetMiner = hit.transform.parent.GetComponentInParent<BitcoinMiner>();
            //        }

            //        if (targetMiner != null) ModLogger.Trace($"[SphereCast] Hit: {targetMiner.name}");
            //        else ModLogger.Trace($"[SphereCast] Hit non-miner: {hit.collider.name}");
            //    }
            //}


            // 距离保底 (Fallback)
            if (targetMiner == null)
            {
                // ModLogger.Debug("未通过射线检测到矿机，尝试通过距离查找最近矿机");
                float minDst = 5.0f;
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

            // 切换上下文
            if (targetMiner != null)
            {
                ModLogger.Debug($"打开矿机界面，切换到矿机: {MinerManager.GetUniqueKey(targetMiner)}");
                if (!MinerManager.Miners.Contains(targetMiner)) MinerManager.Miners.Add(targetMiner);
                MinerManager.SwitchContext(targetMiner);
            }
        }
    }
}