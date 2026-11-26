# DFGE WebUI 测试版 / DFGE WebUI Beta

一款基于 WebView2 + WebUI 界面的 DLSS5 / 多帧生成 / Smooth Motion 开启工具。

- 当前测试版 / Current beta version：**V1.75.0.0**
- 更新内容版本 / Changelog version：**V1.75（V1.65 更新内容 + V1.63.2 历史）**
- 测试版源码分支 / Beta source branch：`beta`
- 稳定版源码分支 / Stable source branch：`master`
- 当前为测试版，功能和界面仍可能调整。

## V1.75 更新内容 / V1.75 Changelog

0. **V1.75 测试版重新发布**
   版本号统一为 V1.75.0.0；外置捕获、OptiScaler 默认项和补丁目录保持当前配置，以下保留 V1.65 更新内容与 V1.63.2 历史记录。

1. **OptiScaler 菜单语言跟随软件语言**
   软件切换到简体中文或 English 后，`CaptureHost\OptiScaler.lang` 自动写入对应语言，OptiScaler 菜单约 0.5 秒内同步；捕获运行中切换也生效，无需重启捕获。

2. **外置捕获正式并入测试版**
   帧生成 mod 新增第三张卡「外置捕获（OptiCaptureHost）」，从这张卡进入即可使用；不向游戏目录写文件，用 Windows Graphics Capture 捕获游戏窗口，再由内置 OptiCaptureHost 2.12.00 + OptiScaler 完成超分和多帧生成。1.63.x 外置捕获测试版的页面、状态、配置写回和 Full Pack 资源全部并入 1.65，独立测试包停止维护。

3. **OptiCaptureHost 2.12.00**
   新增「ReShade 真实深度（实验）」开关与深度接收状态。D3D11 / D3D12 走 GPU 共享纹理，D3D9 / D3D10 / OpenGL / Vulkan 走 CPU 共享缓冲；深度只作用于 Host 自己的 FSR / DLSS 输出缩放，不改善 F5Low / MFG 的果冻感。

4. **捕获默认**
   F5Low + 3X，支持 2X-6X、FSR4.1.2、Present Classic；性能模式默认 Max（Boost=3）；OptiScaler 菜单语言跟随软件设置（见第 1 条），`Ctrl+Shift+F9` 可隐藏 / 恢复覆盖层，热键被占用时页面按钮仍可用。

5. **低延迟默认**
   `[Framerate] LowLatencyInput = optiscaler`、`[XeFG] LowLatencyOutput = reflex`、`[Framerate] FramerateLimit = 0`。0 表示关闭固定上限，避免先降帧再补帧。

6. **无 NGX 定制 Host**
   内置定制 2.12 Host，避免与当前 `nvngx_dlss.dll` 310.9.1 卡在 `NVSDK_NGX_D3D12_Init`；Host 自带 DLSS 输出缩放会回退 FSR1，F5Low / MFG / OptiScaler 主链路不受影响。ReShade 深度测试请把输出缩放选为 FSR3/4。

7. **补丁基线**
   测试版补丁目录以 `DFGE-V1.62[Beta]-纯净完整版` 为准，只保留当前仍在使用的 10 个补丁目录；已淘汰的 20系 / 30系 / N卡 / A卡 / dx9 / 燕云 / RE引擎等旧补丁不再打入测试版。

8. **主页小按钮悬停高亮**
   游戏卡片右侧「多帧生成」「DLSS5」「还原」等小按钮增加高亮边框、渐变背景和明显外发光；暗色主题下绿色光晕更强，开启态、危险态和安装态分别使用绿 / 蓝 / 红 / 琥珀色反馈。
