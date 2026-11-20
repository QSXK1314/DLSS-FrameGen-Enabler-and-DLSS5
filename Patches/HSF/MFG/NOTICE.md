# Third-party notices

## Intel XeSS Frame Generation program data

MFGUnlocker.dll embeds compute shader programs (DXIL) that run the multi-frame model of Intel XeSS Frame Generation on
non-Intel GPUs. 46 of them are derived from programs in Intel's XeSS SDK (`libxess_fg.dll`) and were modified by this
project; the others are this project's own.

For XeMFG on Vulkan it also embeds SPIR-V translations of the programs XeSS Frame Generation creates there: 83
programs, 57 of them translated from programs in Intel's XeSS SDK, the others from this project's own programs.

The Intel-derived program data is Intel's work. It is not covered by this project's MIT License and remains subject to
Intel's licence terms for the XeSS SDK. MFG Unlocker does not include Intel's XeSS SDK binaries, model weights or tensors;
it uses the XeSS Frame Generation copy that comes with the game or OptiScaler.

Intel and XeSS are trademarks of Intel Corporation or its subsidiaries.

## Other components

MFGUnlocker.dll also carries the licences of Dear ImGui, MinHook and Ultimate ASI Loader, which retain their own terms.
