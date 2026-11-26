HSF 增强组件与第三方许可说明
=============================

一、MFG-Unlocker（内置）
------------------------
文件：MFG\MFGUnlocker.dll（V1.7.2）
作者：dashdogy / Michael Robles
项目：https://github.com/dashdogy/MFG-Unlocker
许可：MIT License（见 MFG\LICENSE）
第三方声明：见 MFG\NOTICE.md

本软件只把它复制到 HSF helper 的 mods 目录：
  <游戏目录>\HSF_streamline\mods\MFGUnlocker.dll
用于 Vulkan / DX11 / 32 位游戏的 helper 路线。DX12 原生路线仍需按
MFG-Unlocker 官方说明，把 DLL 改名成游戏会加载的代理名并放到游戏 exe 旁。

二、iMMERSE LaunchPad（不随本软件分发）
---------------------------------------
作者：Pascal Gilcher / Marty McFly
项目：https://github.com/martymcmodding/iMMERSE
许可：Copyright (c) Pascal Gilcher. All rights reserved.
      该许可禁止公共再分发（Public propagation is strictly forbidden）。
      因此本软件不内置 LaunchPad 的 shader / 贴图文件，只在用户点击
      “安装 LaunchPad”时，从上面的官方 GitHub 仓库下载到用户本机，
      再复制到游戏的 reshade-shaders 目录。

安装后的文件：
  <游戏目录>\reshade-shaders\Shaders\MartysMods_LAUNCHPAD.fx
  <游戏目录>\reshade-shaders\Shaders\MartysMods\*.fxh
  <游戏目录>\reshade-shaders\Textures\iMMERSE_bluenoise_*.png
  <游戏目录>\HSF-iMMERSE-LICENSE.txt
  <游戏目录>\HSF-iMMERSE-README.txt

三、HSF
-------
HSF 0.9.9，作者 Capt-Hunter，PolyForm Noncommercial License 1.0.0。
详见 HSF-LICENSE.txt、HSF-README.txt 和 HSF-汉化说明.txt。
