param(
    [string]$Exe = "dist/DistrictJsonToSql.exe"
)

$ErrorActionPreference = "Stop"

# 从 PE 的 RT_MANIFEST 资源（类型 24、ID 1）里读出真正嵌进 exe 的清单，
# 而不是只看源码里的 app.manifest —— 单文件发布后必须以 exe 内的清单为准。
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class PeManifest
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr hFile, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr hModule, IntPtr name, IntPtr type);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hModule);

    public static string Read(string path)
    {
        const uint LoadLibraryAsDataFile = 0x00000002;
        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, LoadLibraryAsDataFile);
        if (module == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            IntPtr info = FindResource(module, new IntPtr(1), new IntPtr(24));
            if (info == IntPtr.Zero)
            {
                return null;
            }

            uint size = SizeofResource(module, info);
            IntPtr data = LockResource(LoadResource(module, info));
            if (data == IntPtr.Zero || size == 0)
            {
                return null;
            }

            byte[] bytes = new byte[size];
            Marshal.Copy(data, bytes, 0, (int)size);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            FreeLibrary(module);
        }
    }
}
'@

$exePath = (Resolve-Path $Exe).Path
$manifest = [PeManifest]::Read($exePath)

if ([string]::IsNullOrWhiteSpace($manifest)) {
    throw "[manifest] 无法从 $exePath 读取 RT_MANIFEST 资源"
}

Write-Host "--- 嵌入的 RT_MANIFEST ---"
Write-Host $manifest
Write-Host "-------------------------"

if ($manifest -notmatch "PerMonitorV2") {
    throw "[manifest] 缺少 PerMonitorV2：进程不是 per-monitor DPI aware，非 100% 缩放会被位图拉伸"
}

if ($manifest -notmatch "true/PM") {
    throw "[manifest] 缺少 dpiAware=true/PM"
}

if ($manifest -notmatch "8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a") {
    throw "[manifest] 缺少 Windows 10/11 的 supportedOS GUID"
}

Write-Host "exe manifest 校验通过：PerMonitorV2 DPI 感知与 Win10/11 supportedOS 已嵌入发布后的 exe。"
