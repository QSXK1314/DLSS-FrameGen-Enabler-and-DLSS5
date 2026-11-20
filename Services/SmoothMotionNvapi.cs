using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DLSSFrameGenEnabler_WinUI3.Services;

/// <summary>
/// 直接通过 NVAPI DRS 读写 NVIDIA 驱动 profile 里的 Smooth Motion 开关。
/// 不依赖 NVIDIA Profile Inspector，也不修改驱动二进制。
///
/// 重要：本机实测 DRS_FindProfileByName / DRS_FindApplicationByName 会返回
/// NVAPI_PROFILE_NOT_FOUND / NVAPI_EXECUTABLE_NOT_FOUND，即使名称完全一致。
/// 因此这里一律走 EnumProfiles + GetProfileInfo + EnumApplications 枚举匹配。
/// </summary>
public static class SmoothMotionNvapi
{
    public const uint SmoothMotionEnableSettingId = 0xB0D384C0;
    public const uint SmoothMotionApisSettingId = 0xB0CC0875;
    public const uint SmoothMotionDebugLogLevelSettingId = 0xB053C379;
    public const uint SmoothMotionDebugBarsSettingId = 0xB01B8B02;
    public const uint SmoothMotionFlipMetering0SettingId = 0xB03A4546;
    public const uint SmoothMotionFlipMetering1SettingId = 0xB03A4547;

    public const int NvapiOk = 0;
    public const int NvapiSettingNotFound = -160;
    public const int NvapiInvalidUserPrivilege = -137;
    public const int NvapiProfileNotFound = -163;
    public const int NvapiExecutableNotFound = -166;
    public const int NvapiAccessDenied = -175;

    private const uint NvapiInitializeId = 0x0150E828;
    private const uint DrsCreateSessionId = 0x0694D52E;
    private const uint DrsDestroySessionId = 0xDAD9CFF8;
    private const uint DrsLoadSettingsId = 0x375DBD6B;
    private const uint DrsSaveSettingsId = 0xFCBC7E14;
    private const uint DrsCreateProfileId = 0xCC176068;
    private const uint DrsDeleteProfileId = 0x17093206;
    private const uint DrsEnumProfilesId = 0xBC371EE0;
    private const uint DrsGetNumProfilesId = 0x1DAE4FBC;
    private const uint DrsCreateApplicationId = 0x4347A9DE;
    private const uint DrsEnumApplicationsId = 0x7FA2173A;
    private const uint DrsSetSettingId = 0x8A2CF5F5;
    private const uint DrsSetSettingFallbackId = 0x577DD202;
    private const uint DrsGetSettingId = 0xEA99498D;
    private const uint DrsGetSettingFallbackId = 0x73BF8338;
    private const uint DrsGetProfileInfoId = 0x61CD6FD6;
    private const uint DrsGetBaseProfileId = 0xDA8466A0;
    private const uint DrsDeleteProfileSettingId = 0xD20D29DF;
    private const uint DrsDeleteProfileSettingFallbackId = 0xE4A26362;
    private const uint SysGetDriverAndBranchVersionId = 0x2926AAAD;

    private const uint NvdrsSettingVer = 0x00013020;
    private const uint NvdrsProfileVer = 0x00011014;
    private const uint NvdrsApplicationVerV4 = 0x0004500C;

    private static readonly object InitLock = new object();
    private static IntPtr _library;
    private static QueryInterfaceDelegate? _queryInterface;
    private static bool _initialized;
    private static string _lastInitError = "";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr QueryInterfaceDelegate(uint functionId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvapiInitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsCreateSessionDelegate(ref IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsDestroySessionDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsLoadSettingsDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsSaveSettingsDelegate(IntPtr session);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsCreateProfileDelegate(IntPtr session, ref NvdrsProfile profile, ref IntPtr profileHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsDeleteProfileDelegate(IntPtr session, IntPtr profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsEnumProfilesDelegate(IntPtr session, uint index, ref IntPtr profileHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsGetNumProfilesDelegate(IntPtr session, ref uint count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsCreateApplicationDelegate(IntPtr session, IntPtr profile, ref NvdrsApplicationV4 application);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsEnumApplicationsDelegate(IntPtr session, IntPtr profile, uint startIndex, ref uint appCount, IntPtr applications);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsSetSettingDelegate(IntPtr session, IntPtr profile, ref NvdrsSetting setting, uint x, uint y);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsGetSettingDelegate(IntPtr session, IntPtr profile, uint settingId, ref NvdrsSetting setting, ref uint x);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsGetProfileInfoDelegate(IntPtr session, IntPtr profile, ref NvdrsProfile profileInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsGetBaseProfileDelegate(IntPtr session, ref IntPtr profileHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrsDeleteProfileSettingDelegate(IntPtr session, IntPtr profile, uint settingId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SysGetDriverAndBranchVersionDelegate(ref uint driverVersion, StringBuilder branchString);

    [StructLayout(LayoutKind.Sequential, Size = 4100)]
    private struct NvdrsSettingUnion
    {
        public uint DwordValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    private struct NvdrsSetting
    {
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string SettingName;
        public uint SettingId;
        public uint SettingType;
        public uint SettingLocation;
        public uint IsCurrentPredefined;
        public uint IsPredefinedValid;
        public NvdrsSettingUnion PredefinedValue;
        public NvdrsSettingUnion CurrentValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    private struct NvdrsProfile
    {
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string ProfileName;
        public uint GpuSupport;
        public uint IsPredefined;
        public uint NumOfApps;
        public uint NumOfSettings;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
    private struct NvdrsApplicationV4
    {
        public uint Version;
        public uint IsPredefined;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string AppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string UserFriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string Launcher;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string FileInFolder;
        public uint BitVector1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string CommandLine;
    }

    private sealed class ProfileEntry
    {
        public IntPtr Handle;
        public string Name = "";
        public uint NumOfApps;
        public uint NumOfSettings;
        public bool IsPredefined;
    }

    public sealed class ProbeResult
    {
        public bool Available;
        public bool SmoothMotionSupported;
        public bool BaseEnabled;
        public int BaseEnableStatus;
        public int BaseApiMaskStatus;
        public uint DriverVersion;
        public string Branch = "";
        public string Error = "";
    }

    public sealed class GameRef
    {
        public string Exe = "";
        public string Name = "";

        public GameRef()
        {
        }

        public GameRef(string exe, string name)
        {
            Exe = exe;
            Name = name;
        }
    }

    public sealed class GameState
    {
        public string Exe = "";
        public string Name = "";
        public string ProfileName = "";
        public bool ProfileFound;
        public bool Enabled;
        public int ApiMask;
        public int EnableStatus;
        public int ApiStatus;
        public string Error = "";
    }

    public sealed class ApplyResult
    {
        public bool Success;
        public string ProfileName = "";
        public bool Enabled;
        public int ApiMask;
        public string Error = "";
        public string Warning = "";
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            _library = NativeLibrary.Load("nvapi64.dll");
            _queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(
                NativeLibrary.GetExport(_library, "nvapi_QueryInterface"));

            NvapiInitializeDelegate initialize = GetFunction<NvapiInitializeDelegate>(NvapiInitializeId);
            int initStatus = initialize();
            if (initStatus != NvapiOk)
            {
                _lastInitError = StatusText(initStatus);
                throw new SmoothMotionException("NvAPI_Initialize 失败：" + StatusText(initStatus));
            }
            _initialized = true;
        }
    }

    private static T GetFunction<T>(uint id) where T : Delegate
    {
        if (_queryInterface == null)
        {
            throw new SmoothMotionException("NVAPI 未初始化");
        }
        IntPtr ptr = _queryInterface(id);
        if (ptr == IntPtr.Zero)
        {
            throw new SmoothMotionException("NVAPI 函数不可用：0x" + id.ToString("X8"));
        }
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private sealed class DrsSession : IDisposable
    {
        public IntPtr Handle;
        private readonly DrsDestroySessionDelegate _destroy;

        public DrsSession()
        {
            DrsCreateSessionDelegate create = GetFunction<DrsCreateSessionDelegate>(DrsCreateSessionId);
            int status = create(ref Handle);
            if (status != NvapiOk || Handle == IntPtr.Zero)
            {
                throw new SmoothMotionException("DRS_CreateSession 失败：" + StatusText(status));
            }

            DrsLoadSettingsDelegate load = GetFunction<DrsLoadSettingsDelegate>(DrsLoadSettingsId);
            status = load(Handle);
            if (status != NvapiOk)
            {
                _destroy = GetFunction<DrsDestroySessionDelegate>(DrsDestroySessionId);
                _destroy(Handle);
                throw new SmoothMotionException("DRS_LoadSettings 失败：" + StatusText(status));
            }

            _destroy = GetFunction<DrsDestroySessionDelegate>(DrsDestroySessionId);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                try
                {
                    _destroy(Handle);
                }
                catch
                {
                }
                Handle = IntPtr.Zero;
            }
        }
    }

    public static ProbeResult Probe()
    {
        ProbeResult result = new ProbeResult();
        try
        {
            EnsureInitialized();
            result.Available = true;

            uint version = 0;
            StringBuilder branch = new StringBuilder(64);
            try
            {
                SysGetDriverAndBranchVersionDelegate getVersion = GetFunction<SysGetDriverAndBranchVersionDelegate>(SysGetDriverAndBranchVersionId);
                int status = getVersion(ref version, branch);
                if (status == NvapiOk)
                {
                    result.DriverVersion = version;
                    result.Branch = branch.ToString();
                }
            }
            catch
            {
            }

            using DrsSession session = new DrsSession();
            IntPtr baseProfile = GetBaseProfile(session.Handle);
            if (baseProfile == IntPtr.Zero)
            {
                result.Error = "找不到基础 profile";
                return result;
            }

            NvdrsSetting setting = ReadSetting(session.Handle, baseProfile, SmoothMotionEnableSettingId);
            result.BaseEnableStatus = ReadRawStatus;
            result.BaseEnabled = setting.CurrentValue.DwordValue != 0;
            result.SmoothMotionSupported = result.BaseEnableStatus == NvapiOk;

            NvdrsSetting apiSetting = ReadSetting(session.Handle, baseProfile, SmoothMotionApisSettingId);
            result.BaseApiMaskStatus = ReadRawStatus;
            return result;
        }
        catch (Exception ex)
        {
            result.Available = false;
            result.Error = ex.Message;
            return result;
        }
    }

    // ReadSetting 需要同时带回返回码；用这个线程静态字段避免每个调用都改结构体。
    [ThreadStatic]
    private static int ReadRawStatus;

    private static NvdrsSetting ReadSetting(IntPtr session, IntPtr profile, uint settingId)
    {
        NvdrsSetting setting = NewSetting(settingId, 0);
        DrsGetSettingDelegate getSetting = GetSettingDelegate();
        uint unused = 0;
        ReadRawStatus = getSetting(session, profile, settingId, ref setting, ref unused);
        return setting;
    }

    private static DrsGetSettingDelegate GetSettingDelegate()
    {
        try
        {
            return GetFunction<DrsGetSettingDelegate>(DrsGetSettingId);
        }
        catch
        {
            return GetFunction<DrsGetSettingDelegate>(DrsGetSettingFallbackId);
        }
    }

    private static DrsSetSettingDelegate GetSetSettingDelegate()
    {
        try
        {
            return GetFunction<DrsSetSettingDelegate>(DrsSetSettingId);
        }
        catch
        {
            return GetFunction<DrsSetSettingDelegate>(DrsSetSettingFallbackId);
        }
    }

    private static DrsDeleteProfileSettingDelegate GetDeleteSettingDelegate()
    {
        try
        {
            return GetFunction<DrsDeleteProfileSettingDelegate>(DrsDeleteProfileSettingId);
        }
        catch
        {
            return GetFunction<DrsDeleteProfileSettingDelegate>(DrsDeleteProfileSettingFallbackId);
        }
    }

    private static NvdrsSetting NewSetting(uint settingId, uint value)
    {
        NvdrsSetting setting = new NvdrsSetting
        {
            Version = NvdrsSettingVer,
            SettingName = "",
            SettingId = settingId,
            SettingType = 0, // DWORD
            SettingLocation = 0, // CURRENT
            IsCurrentPredefined = 0,
            IsPredefinedValid = 0
        };
        setting.PredefinedValue.DwordValue = 0;
        setting.CurrentValue.DwordValue = value;
        return setting;
    }

    private static IntPtr GetBaseProfile(IntPtr session)
    {
        DrsGetBaseProfileDelegate getBase = GetFunction<DrsGetBaseProfileDelegate>(DrsGetBaseProfileId);
        IntPtr handle = IntPtr.Zero;
        int status = getBase(session, ref handle);
        if (status != NvapiOk)
        {
            throw new SmoothMotionException("DRS_GetBaseProfile 失败：" + StatusText(status));
        }
        return handle;
    }

    private static List<ProfileEntry> EnumerateProfiles(IntPtr session)
    {
        DrsGetNumProfilesDelegate getNum = GetFunction<DrsGetNumProfilesDelegate>(DrsGetNumProfilesId);
        uint count = 0;
        int status = getNum(session, ref count);
        if (status != NvapiOk)
        {
            throw new SmoothMotionException("DRS_GetNumProfiles 失败：" + StatusText(status));
        }

        DrsEnumProfilesDelegate enumerate = GetFunction<DrsEnumProfilesDelegate>(DrsEnumProfilesId);
        DrsGetProfileInfoDelegate getInfo = GetFunction<DrsGetProfileInfoDelegate>(DrsGetProfileInfoId);
        List<ProfileEntry> list = new List<ProfileEntry>((int)Math.Min(count, 10000));
        for (uint i = 0; i < count; i++)
        {
            IntPtr handle = IntPtr.Zero;
            status = enumerate(session, i, ref handle);
            if (status != NvapiOk || handle == IntPtr.Zero)
            {
                continue;
            }

            NvdrsProfile info = new NvdrsProfile { Version = NvdrsProfileVer, ProfileName = "" };
            status = getInfo(session, handle, ref info);
            if (status != NvapiOk)
            {
                continue;
            }

            list.Add(new ProfileEntry
            {
                Handle = handle,
                Name = info.ProfileName ?? "",
                NumOfApps = info.NumOfApps,
                NumOfSettings = info.NumOfSettings,
                IsPredefined = info.IsPredefined != 0
            });
        }
        return list;
    }

    private static List<NvdrsApplicationV4> EnumerateApplications(IntPtr session, IntPtr profile, uint countHint)
    {
        int structSize = Marshal.SizeOf<NvdrsApplicationV4>();
        int capacity = Math.Max(16, Math.Min(512, (int)Math.Max(16, countHint)));
        IntPtr buffer = Marshal.AllocHGlobal(structSize * capacity);
        try
        {
            for (int i = 0; i < capacity; i++)
            {
                Marshal.WriteInt32(buffer + i * structSize, (int)NvdrsApplicationVerV4);
            }

            uint count = (uint)capacity;
            DrsEnumApplicationsDelegate enumerate = GetFunction<DrsEnumApplicationsDelegate>(DrsEnumApplicationsId);
            int status = enumerate(session, profile, 0, ref count, buffer);
            if (status != NvapiOk)
            {
                return new List<NvdrsApplicationV4>();
            }

            int actual = (int)Math.Min(count, (uint)capacity);
            List<NvdrsApplicationV4> apps = new List<NvdrsApplicationV4>(actual);
            for (int i = 0; i < actual; i++)
            {
                NvdrsApplicationV4 app = Marshal.PtrToStructure<NvdrsApplicationV4>(buffer + i * structSize);
                apps.Add(app);
            }
            return apps;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }
        string lower = name.ToLowerInvariant();
        lower = lower.Replace("&", "and");
        lower = Regex.Replace(lower, @"[^\p{L}\p{N}]", "");
        return lower;
    }

    private static bool AppsContainExe(List<NvdrsApplicationV4> apps, string exe)
    {
        string exeName = Path.GetFileName(exe).ToLowerInvariant();
        if (exeName.Length == 0)
        {
            return false;
        }
        foreach (NvdrsApplicationV4 app in apps)
        {
            string appName = (app.AppName ?? "").ToLowerInvariant();
            string fileInFolder = (app.FileInFolder ?? "").ToLowerInvariant();
            if (appName == exeName || Path.GetFileName(appName) == exeName)
            {
                return true;
            }
            if (fileInFolder.Length > 0 && (fileInFolder == exeName || Path.GetFileName(fileInFolder) == exeName))
            {
                return true;
            }
        }
        return false;
    }

    private static ProfileEntry? FindProfile(
        List<ProfileEntry> profiles,
        IntPtr session,
        string exe,
        string gameName,
        out List<NvdrsApplicationV4> matchedApps)
    {
        matchedApps = new List<NvdrsApplicationV4>();

        string englishName = "";
        try
        {
            englishName = GameNameDatabase.GetGameName(exe, true) ?? "";
        }
        catch
        {
        }

        string[] candidates = new[] { gameName, englishName, Path.GetFileNameWithoutExtension(exe) }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] normalizedCandidates = candidates.Select(NormalizeName).Where(s => s.Length > 0).ToArray();

        // 1) 精确名称匹配
        foreach (ProfileEntry profile in profiles)
        {
            string normalizedProfile = NormalizeName(profile.Name);
            if (normalizedProfile.Length == 0)
            {
                continue;
            }
            foreach (string candidate in normalizedCandidates)
            {
                if (normalizedProfile == candidate)
                {
                    List<NvdrsApplicationV4> apps = EnumerateApplications(session, profile.Handle, profile.NumOfApps);
                    if (profile.NumOfApps == 0 || AppsContainExe(apps, exe))
                    {
                        matchedApps = apps;
                        return profile;
                    }
                }
            }
        }

        // 2) 包含匹配（名称足够长时才启用，避免 "Wolfenstein" 误命中所有系列）
        foreach (ProfileEntry profile in profiles)
        {
            string normalizedProfile = NormalizeName(profile.Name);
            if (normalizedProfile.Length < 5)
            {
                continue;
            }
            foreach (string candidate in normalizedCandidates)
            {
                if (candidate.Length < 5)
                {
                    continue;
                }
                if (normalizedProfile.Contains(candidate) || candidate.Contains(normalizedProfile))
                {
                    List<NvdrsApplicationV4> apps = EnumerateApplications(session, profile.Handle, profile.NumOfApps);
                    if (AppsContainExe(apps, exe))
                    {
                        matchedApps = apps;
                        return profile;
                    }
                }
            }
        }

        // 3) 不再遍历全部 7991 个 profile 的 app 列表：那会在页面刷新时拖到十几秒。
        // 名称匹配不到时由 Apply 创建新 profile；如果该 exe 已被别的 profile 占用，
        // CreateApplication 会明确报错，届时前端会显示错误而不是卡住。
        return null;
    }

    public static List<GameState> ReadGames(IEnumerable<GameRef> games)
    {
        EnsureInitialized();
        using DrsSession session = new DrsSession();
        List<ProfileEntry> profiles = EnumerateProfiles(session.Handle);
        List<GameState> result = new List<GameState>();

        foreach (GameRef game in games)
        {
            GameState state = new GameState { Exe = game.Exe, Name = game.Name };
            try
            {
                List<NvdrsApplicationV4> apps;
                ProfileEntry? profile = FindProfile(profiles, session.Handle, game.Exe, game.Name, out apps);
                if (profile == null)
                {
                    state.ProfileFound = false;
                    state.Enabled = false;
                    state.ApiMask = 7;
                    state.Error = "";
                    result.Add(state);
                    continue;
                }

                state.ProfileFound = true;
                state.ProfileName = profile.Name;

                NvdrsSetting enable = ReadSetting(session.Handle, profile.Handle, SmoothMotionEnableSettingId);
                state.EnableStatus = ReadRawStatus;
                state.Enabled = ReadRawStatus == NvapiOk && enable.CurrentValue.DwordValue != 0;

                NvdrsSetting apis = ReadSetting(session.Handle, profile.Handle, SmoothMotionApisSettingId);
                state.ApiStatus = ReadRawStatus;
                state.ApiMask = ReadRawStatus == NvapiOk ? (int)apis.CurrentValue.DwordValue : 7;
                if (state.ApiMask <= 0)
                {
                    state.ApiMask = 7;
                }
            }
            catch (Exception ex)
            {
                state.Error = ex.Message;
            }
            result.Add(state);
        }

        return result;
    }

    public static ApplyResult Apply(string exe, string name, bool enabled, int apiMask)
    {
        ApplyResult result = new ApplyResult { Enabled = enabled, ApiMask = apiMask };
        try
        {
            EnsureInitialized();
            using DrsSession session = new DrsSession();
            List<ProfileEntry> profiles = EnumerateProfiles(session.Handle);
            List<NvdrsApplicationV4> apps;
            ProfileEntry? found = FindProfile(profiles, session.Handle, exe, name, out apps);

            bool created = false;
            ProfileEntry profile;
            if (found != null)
            {
                profile = found;
            }
            else
            {
                string profileName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(exe) : name.Trim();
                DrsCreateProfileDelegate create = GetFunction<DrsCreateProfileDelegate>(DrsCreateProfileId);
                NvdrsProfile newProfile = new NvdrsProfile
                {
                    Version = NvdrsProfileVer,
                    ProfileName = profileName,
                    GpuSupport = 0,
                    IsPredefined = 0,
                    NumOfApps = 0,
                    NumOfSettings = 0
                };
                IntPtr handle = IntPtr.Zero;
                int createStatus = create(session.Handle, ref newProfile, ref handle);
                if (createStatus != NvapiOk || handle == IntPtr.Zero)
                {
                    result.Error = "创建 profile 失败：" + StatusText(createStatus);
                    return result;
                }

                profile = new ProfileEntry
                {
                    Handle = handle,
                    Name = profileName,
                    NumOfApps = 0,
                    NumOfSettings = 0,
                    IsPredefined = false
                };
                created = true;
            }

            result.ProfileName = profile.Name;

            // profile 里没有这个 exe 时补进去。已有同名 profile 但 app 不同，也要补。
            bool appExists = AppsContainExe(apps, exe);
            if (!appExists)
            {
                DrsCreateApplicationDelegate createApp = GetFunction<DrsCreateApplicationDelegate>(DrsCreateApplicationId);
                NvdrsApplicationV4 app = new NvdrsApplicationV4
                {
                    Version = NvdrsApplicationVerV4,
                    IsPredefined = 0,
                    AppName = exe,
                    UserFriendlyName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(exe) : name,
                    Launcher = "",
                    FileInFolder = Path.GetFileName(exe),
                    BitVector1 = 0,
                    CommandLine = ""
                };
                int appStatus = createApp(session.Handle, profile.Handle, ref app);
                if (appStatus != NvapiOk)
                {
                    if (created)
                    {
                        try
                        {
                            DrsDeleteProfileDelegate delete = GetFunction<DrsDeleteProfileDelegate>(DrsDeleteProfileId);
                            delete(session.Handle, profile.Handle);
                        }
                        catch
                        {
                        }
                    }
                    result.Error = "把游戏 exe 加入 profile 失败：" + StatusText(appStatus);
                    return result;
                }
            }

            DrsSetSettingDelegate setSetting = GetSetSettingDelegate();
            NvdrsSetting enable = NewSetting(SmoothMotionEnableSettingId, enabled ? 1u : 0u);
            int status = setSetting(session.Handle, profile.Handle, ref enable, 0, 0);
            if (status != NvapiOk)
            {
                result.Error = "写入 Smooth Motion 开关失败：" + StatusText(status);
                return result;
            }

            int apiStatus = NvapiOk;
            // Enabled APIs 在部分驱动/预定义 profile 上是受保护设置，普通权限会返回 -137。
            // 7 是驱动默认的“所有 API 都允许”，不写也能达到同样效果，避免无意义的告警。
            if (enabled && apiMask > 0 && apiMask != 7)
            {
                NvdrsSetting apis = NewSetting(SmoothMotionApisSettingId, (uint)apiMask);
                apiStatus = setSetting(session.Handle, profile.Handle, ref apis, 0, 0);
            }

            DrsSaveSettingsDelegate save = GetFunction<DrsSaveSettingsDelegate>(DrsSaveSettingsId);
            status = save(session.Handle);
            if (status != NvapiOk)
            {
                result.Error = "保存驱动 profile 失败：" + StatusText(status);
                return result;
            }

            result.Success = true;
            result.ProfileName = profile.Name;
            if (apiStatus != NvapiOk && enabled)
            {
                result.Warning = "开关已写入，但 Enabled APIs 写入失败：" + StatusText(apiStatus);
                if (apiStatus == NvapiInvalidUserPrivilege)
                {
                    result.Warning += "（需要管理员权限）";
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            return result;
        }
    }

    public static ApplyResult Reset(string exe, string name)
    {
        ApplyResult result = new ApplyResult { Success = false };
        try
        {
            EnsureInitialized();
            using DrsSession session = new DrsSession();
            List<ProfileEntry> profiles = EnumerateProfiles(session.Handle);
            List<NvdrsApplicationV4> apps;
            ProfileEntry? profile = FindProfile(profiles, session.Handle, exe, name, out apps);
            if (profile == null)
            {
                result.Success = true;
                result.Error = "";
                result.Warning = "没有找到这个游戏的驱动 profile，当前就是默认状态";
                return result;
            }

            result.ProfileName = profile.Name;
            DrsDeleteProfileSettingDelegate deleteSetting = GetDeleteSettingDelegate();
            int enableStatus = deleteSetting(session.Handle, profile.Handle, SmoothMotionEnableSettingId);
            int apiStatus = deleteSetting(session.Handle, profile.Handle, SmoothMotionApisSettingId);

            // 即使 APIs 删除被驱动拒绝，也要先保存已经删掉的开关，否则会停在半还原状态。
            DrsSaveSettingsDelegate save = GetFunction<DrsSaveSettingsDelegate>(DrsSaveSettingsId);
            int saveStatus = save(session.Handle);
            if (saveStatus != NvapiOk)
            {
                result.Error = "保存驱动 profile 失败：" + StatusText(saveStatus);
                return result;
            }

            if (enableStatus != NvapiOk && enableStatus != NvapiSettingNotFound)
            {
                result.Error = "删除 Smooth Motion 开关失败：" + StatusText(enableStatus);
                return result;
            }

            result.Success = true;
            if (apiStatus == NvapiInvalidUserPrivilege)
            {
                result.Warning = "Enabled APIs 未能删除：需要管理员权限";
            }
            else if (apiStatus != NvapiOk && apiStatus != NvapiSettingNotFound && apiStatus != NvapiInvalidUserPrivilege)
            {
                result.Warning = "开关已恢复默认，但 Enabled APIs 未能删除：" + StatusText(apiStatus);
            }
            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            return result;
        }
    }

    public static string StatusText(int status)
    {
        switch (status)
        {
            case 0:
                return "NVAPI_OK";
            case -160:
                return "NVAPI_SETTING_NOT_FOUND (-160)";
            case -137:
                return "NVAPI_INVALID_USER_PRIVILEGE (-137，需要管理员权限)";
            case -163:
                return "NVAPI_PROFILE_NOT_FOUND (-163)";
            case -166:
                return "NVAPI_EXECUTABLE_NOT_FOUND (-166)";
            case -175:
                return "NVAPI_ACCESS_DENIED (-175)";
            default:
                return "NVAPI_" + status;
        }
    }
}

public sealed class SmoothMotionException : Exception
{
    public SmoothMotionException(string message) : base(message)
    {
    }
}
