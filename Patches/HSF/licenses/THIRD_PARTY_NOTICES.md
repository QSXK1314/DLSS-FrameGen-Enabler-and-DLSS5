# Third-party notices

## In this repository

| Component | Where | Licence |
|---|---|---|
| ReShade add-on API headers, tag v6.0.0, by Patrick Mours | `HSF/third_party/reshade/include` | BSD-3-Clause, see [`HSF/third_party/reshade/LICENSE.md`](HSF/third_party/reshade/LICENSE.md) |
| Dear ImGui headers (`imgui.h`, `imconfig.h`), by Omar Cornut | `HSF/third_party/imgui` | MIT, see [`HSF/third_party/imgui/LICENSE.txt`](HSF/third_party/imgui/LICENSE.txt) |
| MinHook, by Tsuda Kageyu (commit c3fcafd) | `HSF/third_party/minhook` | BSD-2-Clause, see [`HSF/third_party/minhook/LICENSE.txt`](HSF/third_party/minhook/LICENSE.txt) |
| GPU fences for Direct3D 9Ex, ported from RenoDX's `src/utils/gpu_fence.hpp` by Carlos Lopez (ShortFuse), commit d97ad70 | `HSF/src/gpu_fence9.cpp`, `HSF/src/gpu_fence9.h` | MIT, see [`HSF/third_party/renodx/LICENSE`](HSF/third_party/renodx/LICENSE) |
| Colour conversion for DLSS-G from FP16 (scRGB): the BT.709 to BT.2020 matrices, the ST.2084 (PQ) and sRGB curves, from RenoDX's `src/shaders/color/rgb.hlsl`, `pq.hlsl` and `srgb.hlsl`, and its HDR display check and colour space mapping (`ChangeColorSpace`) from `src/utils/swapchain.hpp`, by Carlos Lopez (ShortFuse), commit 216fc64 | `HSF/src/hdr_out.cpp`, `HSF/src/dlssg_core.cpp`, `HSF/src/hsf_addon.cpp` | MIT, see [`HSF/third_party/renodx/LICENSE`](HSF/third_party/renodx/LICENSE) |
| NVIDIA Optical Flow as a motion source, ported from DLSS5 ReShade AIO's `addon/src/nvof-motion-provider.cpp` (Copyright 2026 kibblerz, release 2.2.3, commit 09301f5) and changed for HSF | `HSF/src/nvof_flow.cpp`, `HSF/src/nvof_flow.h` | Apache-2.0, see [`HSF/third_party/kibblerz-aio/LICENSE`](HSF/third_party/kibblerz-aio/LICENSE) and [`NOTICE`](HSF/third_party/kibblerz-aio/NOTICE) |
| NVIDIA Optical Flow SDK 5.0 interface headers (`nvOpticalFlowCommon.h`, `nvOpticalFlowD3D12.h`), by NVIDIA. The engine itself is the display driver's `nvofapi64.dll`, never shipped | `HSF/third_party/nvof` | NVIDIA's permissive licence, in each header |
| Vulkan-Headers (`VK_HEADER_VERSION` 296), by The Khronos Group | `HSF/third_party/vulkan-headers` | Apache-2.0 or MIT, see [`HSF/third_party/vulkan-headers/LICENSE.md`](HSF/third_party/vulkan-headers/LICENSE.md) |

## Approaches learned from

Credited for an approach HSF follows.

| Project | What HSF follows | Licence |
|---|---|---|
| [AeonSR](https://github.com/BarbatosAWLS/AeonSR), by BarbatosAWLS | on the Vulkan route, DLSS-G presents from a window of its own over the game's, not a child window (`overlay_presenter.cpp`), in `HSF/src/dlssg_core.cpp` | MIT |

## Needed to build, not in this repository

| Component | Use | Licence |
|---|---|---|
| NVIDIA Streamline SDK 2.14 | headers and `sl.interposer.lib` at build time; its DLLs ship in `HSF_streamline` | Streamline core: MIT. The DLSS-G binaries (`sl.dlss_g.dll`, `nvngx_dlssg.dll`) are under NVIDIA's own terms, which come with the SDK. |

## Used alongside HSF, never shipped with it

Players install these themselves. HSF only reads what they produce, and none of their code or files are in this repository or in an HSF release.

| Tool | What HSF uses it for |
|---|---|
| ReShade | the host for the HSF add-on |
| iMMERSE LaunchPad, by Marty McFly | optical flow and depth for games without DLSS |
| LumeniteFX | optical flow (Kernel) |
| GITC Uplift, by ghostinthecamera | its NR runs alongside HSF |
| ShortFuse's NR mod, DFC NR | run alongside HSF |
| MFG-Unlocker, by dashdogy | multi-frame generation on RTX 20, 30 and 40 cards |
| RTSS | its overlay is kept out of generated frames |
| Luma, by Pumbo (Filoppi) and contributors | in D3D11 games it adds DLSS to, HSF reads the motion vectors and depth Luma hands to DLSS, and feeds them to frame generation |
