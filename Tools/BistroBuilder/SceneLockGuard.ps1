param(
    [string]$Path = "",
    [switch]$RepairKnownResidual
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
if ([string]::IsNullOrWhiteSpace($Path)) {
    $Path = Join-Path $projectRoot 'Assets\Scenes\Prototype_Restaurant.unity'
}
$fullPath = [IO.Path]::GetFullPath($Path)

function Test-ExclusiveFileAccess([string]$FilePath) {
    try {
        $stream = [IO.File]::Open(
            $FilePath,
            [IO.FileMode]::Open,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
        $stream.Dispose()
        return $true
    }
    catch {
        return $false
    }
}

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class BBSceneLockNative
{
    private const int SystemExtendedHandleInformation = 64;
    private const uint ProcessDuplicateHandle = 0x40;
    private const uint DuplicateCloseSource = 1;
    private const uint DuplicateSameAccess = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct UniqueProcess
    {
        public int ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME StartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessInfo
    {
        public UniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string ServiceName;
        public uint ApplicationType;
        public uint AppStatus;
        public uint SessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedHandle
    {
        public IntPtr Object;
        public IntPtr ProcessId;
        public IntPtr HandleValue;
        public uint GrantedAccess;
        public ushort CreatorBackTraceIndex;
        public ushort ObjectTypeIndex;
        public uint HandleAttributes;
        public uint Reserved;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(
        out uint handle, int flags, string sessionKey);
    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint handle);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint handle, uint fileCount, string[] files,
        uint appCount, IntPtr apps, uint serviceCount, string[] services);
    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint handle, out uint needed, ref uint count,
        [In, Out] ProcessInfo[] processes, ref uint rebootReasons);

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        int infoClass, IntPtr info, int length, ref int returnLength);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint access, bool inheritHandle, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DuplicateHandle(
        IntPtr sourceProcess, IntPtr sourceHandle,
        IntPtr targetProcess, out IntPtr targetHandle,
        uint desiredAccess, bool inheritHandle, uint options);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandle(
        IntPtr handle, StringBuilder path, uint pathLength, uint flags);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static int[] GetLockingProcessIds(string filePath)
    {
        uint session;
        int result = RmStartSession(
            out session, 0, Guid.NewGuid().ToString("N"));
        if (result != 0) return new int[0];
        try
        {
            RmRegisterResources(
                session, 1, new[] { filePath },
                0, IntPtr.Zero, 0, null);
            uint needed = 0;
            uint count = 0;
            uint reboot = 0;
            result = RmGetList(
                session, out needed, ref count, null, ref reboot);
            if (result == 0) return new int[0];
            if (result != 234 || needed == 0) return new int[0];

            ProcessInfo[] processes = new ProcessInfo[needed];
            count = needed;
            result = RmGetList(
                session, out needed, ref count, processes, ref reboot);
            if (result != 0) return new int[0];

            int[] ids = new int[count];
            for (int index = 0; index < count; index++)
                ids[index] = processes[index].Process.ProcessId;
            return ids;
        }
        finally
        {
            RmEndSession(session);
        }
    }

    private static IEnumerable<IntPtr> EnumerateHandles(int processId)
    {
        int length = 0x10000;
        int returnLength = 0;
        IntPtr buffer = Marshal.AllocHGlobal(length);
        try
        {
            while (true)
            {
                int status = NtQuerySystemInformation(
                    SystemExtendedHandleInformation,
                    buffer, length, ref returnLength);
                if (status == 0) break;
                if (status != unchecked((int)0xC0000004)) yield break;
                Marshal.FreeHGlobal(buffer);
                length = Math.Max(length * 2, returnLength);
                buffer = Marshal.AllocHGlobal(length);
            }

            long count = IntPtr.Size == 8
                ? Marshal.ReadInt64(buffer)
                : Marshal.ReadInt32(buffer);
            IntPtr cursor = IntPtr.Add(buffer, IntPtr.Size * 2);
            int size = Marshal.SizeOf(typeof(ExtendedHandle));

            for (long index = 0; index < count; index++)
            {
                ExtendedHandle entry = (ExtendedHandle)
                    Marshal.PtrToStructure(cursor, typeof(ExtendedHandle));
                if (entry.ProcessId.ToInt64() == processId)
                    yield return entry.HandleValue;
                cursor = IntPtr.Add(cursor, size);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ResolvePath(IntPtr handle)
    {
        StringBuilder builder = new StringBuilder(4096);
        uint length = GetFinalPathNameByHandle(
            handle, builder, (uint)builder.Capacity, 0);
        if (length == 0 || length >= builder.Capacity) return null;
        string value = builder.ToString();
        return value.StartsWith("\\\\?\\", StringComparison.Ordinal)
            ? value.Substring(4)
            : value;
    }

    public static bool CloseExactFileHandle(
        int processId,
        string filePath)
    {
        string target = System.IO.Path.GetFullPath(filePath);
        IntPtr process = OpenProcess(
            ProcessDuplicateHandle, false, processId);
        if (process == IntPtr.Zero) return false;
        try
        {
            foreach (IntPtr sourceHandle in EnumerateHandles(processId))
            {
                IntPtr duplicate;
                if (!DuplicateHandle(
                        process, sourceHandle, GetCurrentProcess(),
                        out duplicate, 0, false, DuplicateSameAccess))
                    continue;

                string candidate = null;
                try { candidate = ResolvePath(duplicate); }
                catch { }
                finally { CloseHandle(duplicate); }
                if (candidate == null) continue;

                try { candidate = System.IO.Path.GetFullPath(candidate); }
                catch { continue; }

                if (!string.Equals(
                        candidate, target,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                IntPtr closedDuplicate;
                bool closed = DuplicateHandle(
                    process,
                    sourceHandle,
                    GetCurrentProcess(),
                    out closedDuplicate,
                    0,
                    false,
                    DuplicateCloseSource | DuplicateSameAccess);
                if (closedDuplicate != IntPtr.Zero)
                    CloseHandle(closedDuplicate);
                return closed;
            }

            return false;
        }
        finally
        {
            CloseHandle(process);
        }
    }
}
'@

if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
    Write-Error "BB_SCENE_LOCK_GUARD|MISSING|$fullPath"
    exit 3
}

if (Test-ExclusiveFileAccess $fullPath) {
    Write-Output "BB_SCENE_LOCK_GUARD|PASS|$fullPath"
    exit 0
}

$lockingIds = [BBSceneLockNative]::GetLockingProcessIds($fullPath)
if ($lockingIds.Count -eq 0) {
    Write-Error "BB_SCENE_LOCK_GUARD|LOCKED|OWNER_UNKNOWN|$fullPath"
    exit 2
}

$knownResidualRepaired = $false
foreach ($processId in $lockingIds) {
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        Write-Warning "BB_SCENE_LOCK_GUARD|OWNER|$processId|PROCESS_GONE"
        continue
    }

    $commandLine = [string]$process.CommandLine
    $isDesktopCommander =
        $process.Name -ieq 'node.exe' -and
        $commandLine -match 'desktop-commander'

    Write-Output (
        "BB_SCENE_LOCK_GUARD|OWNER|{0}|{1}|KNOWN={2}" -f
        $processId, $process.Name, $isDesktopCommander)

    if (-not $RepairKnownResidual -or -not $isDesktopCommander) {
        continue
    }

    if ([BBSceneLockNative]::CloseExactFileHandle(
            $processId, $fullPath)) {
        Write-Output (
            "BB_SCENE_LOCK_GUARD|REPAIRED|{0}|DESKTOP_COMMANDER_HANDLE" -f
            $processId)
        $knownResidualRepaired = $true
    }
}

Start-Sleep -Milliseconds 100
if (Test-ExclusiveFileAccess $fullPath) {
    if ($knownResidualRepaired) { $state = 'PASS_AFTER_REPAIR' } else { $state = 'PASS_AFTER_RELEASE' }
    Write-Output "BB_SCENE_LOCK_GUARD|$state|$fullPath"
    exit 0
}

Write-Error (
    "BB_SCENE_LOCK_GUARD|LOCKED|NO_SAFE_REPAIR|" +
    $fullPath)
exit 2
