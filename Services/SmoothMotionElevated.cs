using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DLSSFrameGenEnabler_WinUI3.Services;

/// <summary>
/// 以管理员权限执行一次 Smooth Motion 的驱动 profile 写入。
///
/// Enabled APIs（0xB0CC0875）在 NVIDIA 驱动里属于受保护设置，普通权限写入会返回
/// NVAPI_INVALID_USER_PRIVILEGE (-137)。这里不把整个主程序改成 requireAdministrator，
/// 只在用户选择单个 API、确实需要写这一项时，由主程序用 runas 启动自身的一个短命进程，
/// 写完结果后立即退出，避免每次启动软件都弹 UAC，也不影响主界面的拖放。
/// </summary>
public static class SmoothMotionElevated
{
    public const string ApplySwitch = "--smooth-apply-elevated";
    public const string ResetSwitch = "--smooth-reset-elevated";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true
    };

    public static bool IsElevatedRequest(string[] args)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, ApplySwitch, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, ResetSwitch, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 处理管理员模式的一次性请求。返回 true 表示参数已被本类接管，调用方应直接退出。
    /// </summary>
    public static bool TryHandle(string[] args)
    {
        if (!IsElevatedRequest(args))
        {
            return false;
        }

        bool reset = HasSwitch(args, ResetSwitch);
        string exe = GetValue(args, "--exe");
        string name = GetValue(args, "--name");
        string resultPath = GetValue(args, "--result");
        bool enabled = GetValue(args, "--enabled") is "1" or "true" or "True";
        int apiMask = 7;
        string apiText = GetValue(args, "--apis");
        if (!string.IsNullOrWhiteSpace(apiText))
        {
            try
            {
                apiMask = int.Parse(apiText);
            }
            catch
            {
                apiMask = 7;
            }
        }

        SmoothMotionNvapi.ApplyResult result;
        try
        {
            result = reset
                ? SmoothMotionNvapi.Reset(exe, name)
                : SmoothMotionNvapi.Apply(exe, name, enabled, apiMask);
        }
        catch (Exception ex)
        {
            result = new SmoothMotionNvapi.ApplyResult
            {
                Success = false,
                Enabled = enabled,
                ApiMask = apiMask,
                Error = ex.Message
            };
        }

        if (!string.IsNullOrWhiteSpace(resultPath))
        {
            try
            {
                File.WriteAllText(
                    resultPath,
                    JsonSerializer.Serialize(result, JsonOptions),
                    new UTF8Encoding(false));
            }
            catch
            {
                // 结果写不出去时由主程序按“没有返回结果”处理。
            }
        }

        return true;
    }

    private static bool HasSwitch(string[] args, string switchName)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, switchName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return "";
    }
}
