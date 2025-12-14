using Duckov.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoreBtcMiner.Core   // 这里改成自己的命名空间
{
    /// <summary>
    /// 通用的支持多依赖的 MOD 加载器基类。
    /// <para>继承此类，重写 GetDependencies() 和 CreateImplementation() 即可。</para>
    /// </summary>
    public abstract class DependencyModLoader : Duckov.Modding.ModBehaviour
    {
        // 使用 HashSet 存储还需要等待的依赖，方便快速查找和移除
        private HashSet<string> _missingDependencies = default!;
        private bool _isLoaded = false;
        private MonoBehaviour _implementation = default!;

        /// <summary>
        /// 【必须重写】返回此前置 MOD 依赖的名称列表（对应 info.ini 中的 name）
        /// </summary>
        protected abstract string[] GetDependencies();

        /// <summary>
        /// 【必须重写】当所有依赖就绪后，挂载真正的业务组件
        /// </summary>
        /// <param name="master">官方 ModManager 实例</param>
        /// <param name="info">官方 ModInfo 实例</param>
        /// <returns>返回挂载好的业务组件</returns>
        protected abstract MonoBehaviour CreateImplementation(ModManager master, ModInfo info);

        protected override void OnAfterSetup()
        {
            // 获取子类定义的依赖列表
            var required = GetDependencies();
            if (required == null || required.Length == 0)
            {
                // 没有依赖，直接启动
                TryInitImplementation();
                return;
            }

            // 初始化缺失列表
            _missingDependencies = [.. required];

            // 检查当前内存中已经存在的程序集
            // 通过检查 AppDomain 来判断依赖是否已经加载
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                                    .Select(a => a.GetName().Name)
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase); // 忽略大小写

            // 移除那些已经加载的依赖
            _missingDependencies.RemoveWhere(loadedAssemblies.Contains);

            // 判断是否全部就绪
            if (_missingDependencies.Count == 0)
            {
                TryInitImplementation();
            }
            else
            {
                // 还有没加载的，订阅官方事件
                Debug.Log($"[{info.name}] 等待依赖: {string.Join(", ", _missingDependencies)}");
                ModManager.OnModActivated += OnModActivatedHandler;
            }
        }

        private void OnModActivatedHandler(ModInfo activatedModInfo, Duckov.Modding.ModBehaviour modBehaviour)
        {
            // 检查新激活的 MOD 是否在等待名单里
            if (_missingDependencies.Contains(activatedModInfo.name))
            {
                _missingDependencies.Remove(activatedModInfo.name);
                Debug.Log($"[{info.name}] 依赖 {activatedModInfo.name} 已加载。剩余: {_missingDependencies.Count}");

                if (_missingDependencies.Count == 0)
                {
                    // 全部到齐，取消订阅并启动
                    ModManager.OnModActivated -= OnModActivatedHandler;
                    TryInitImplementation();
                }
            }
        }

        private void TryInitImplementation()
        {
            if (_isLoaded) return;

            Debug.Log($"[{info.name}] 所有依赖已就绪，启动业务逻辑。");

            // 调用子类的实现来创建组件
            _implementation = CreateImplementation(this.master, this.info);
            _isLoaded = true;
        }

        protected override void OnBeforeDeactivate()
        {
            // 清理事件监听，防止泄漏
            ModManager.OnModActivated -= OnModActivatedHandler;

            // 尝试通知业务组件停用
            _implementation?.SendMessage("ManualDeactivate", SendMessageOptions.DontRequireReceiver);
        }
    }
}