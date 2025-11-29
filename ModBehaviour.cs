using JmcModLib.Config;
using JmcModLib.Config.UI;
using JmcModLib.Core;
using JmcModLib.Utils;
using MoreBtcMiner.Core;
using MoreBtcMiner.Patches;

namespace MoreBtcMiner
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        [UIIntSlider(0, 10)]
        [Config("矿机建造上限2")]
        public static int MaxMinerCount = 2; // 默认值设为 10

        private readonly HarmonyHelper harmonyHelper = new($"{VersionInfo.Name}");
        private void OnEnable()
        {
        }
        private void OnDisable()
        {
            ModLogger.Info("Mod 即将禁用，配置已保存");
            harmonyHelper.OnDisable();
        }

        protected override void OnAfterSetup()
        {
            ModRegistry.Register(true, info, VersionInfo.Name, VersionInfo.Version)?
                       .RegisterL10n()
                       .RegisterLogger(uIFlags: LogConfigUIFlags.All)
                       .Done();
            harmonyHelper.OnEnable();
        }

        protected override void OnBeforeDeactivate()
        {
            ModLogger.Info("Mod 已禁用，配置已保存");
        }
    }
}
