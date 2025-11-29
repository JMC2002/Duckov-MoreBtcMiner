using JmcModLib.Config;
using JmcModLib.Config.UI;

namespace MoreBtcMiner.Core
{
    public static class Setting
    {
        // 定义配置项
        // 参数说明：[显示名称, 描述(Tooltip), 分组]
        [UIIntSlider(0, 10)]
        [Config("矿机建造上限")]
        public static int MaxMinerCount = 2; // 默认值设为 10
    }
}
