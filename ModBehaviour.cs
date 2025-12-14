using Duckov.Modding;
using MoreBtcMiner.Core;
using UnityEngine;

namespace MoreBtcMiner
{
    public class ModBehaviour : DependencyModLoader
    {
        protected override string[] GetDependencies()
        {
            return
            [
                "JmcModLib"
            ];
        }

        // 挂载实际业务脚本
        protected override MonoBehaviour CreateImplementation(ModManager master, ModInfo info)
        {
            // 1. 挂载组件
            var impl = this.gameObject.AddComponent<ModBehaviourImpl>();

            impl.Setup(master, info);

            return impl;
        }
    }
}
