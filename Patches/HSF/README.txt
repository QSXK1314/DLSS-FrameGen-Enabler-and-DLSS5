HSF 0.9.9 (preview)

Frame generation for games that lack it: a ReShade add-on that feeds NVIDIA DLSS Frame Generation.
DirectX 9, 11 and 12 and Vulkan, 32-bit and 64-bit, SDR and HDR.

Install (<game> is the folder that holds the game's exe):
1. Install ReShade 6.8 with full add-on support into <game>.
2. Copy hsf.addon64 (64-bit game) or hsf.addon32 (32-bit game) into <game>.
3. Copy the HSF_streamline folder into <game> (32-bit games need it too).
4. Copy the reshade-shaders folder into <game> (it adds HSF.fx).
5. DirectX 9/11 and Vulkan games, and DirectX 12 games without DLSS, need a flow source you get yourself:
   iMMERSE LaunchPad or LumeniteFX Kernel.
6. Copy HSF.ini into <game> too, replacing the old one: it is a clean start. DirectX 12 games: restart once when the menu asks.

Needs an RTX 40 or newer GPU (RTX 20/30 through MFG-Unlocker, installed by you).
Never use HSF in online games with anti-cheat.

Full guide: docs/user-guide.md in the repository.
Licences of the bundled NVIDIA Streamline files and of the code HSF uses: the licenses folder.
