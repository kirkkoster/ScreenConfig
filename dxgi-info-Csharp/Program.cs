using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using dxgi_info_Csharp.models;
using Newtonsoft.Json;
using SharpDX;
using SharpDX.DXGI;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            DisplayUsage();
            return;
        }

        string command = args[0].ToLower();

        switch (command)
        {
            case "usage":
                DisplayUsage();
                break;

            case "listmonitors":
                ListMonitors();
                break;

            case "setmonitor":
                SetMonitorCommand(args);
                break;

            case "deviceinfo":
                ShowDeviceInfo(args);
                break;

            case "resetmonitor":
                ResetDisplaySettings();
                break;

            case "listdisplaymodes":
                ListDisplayModesCommand(args);
                break;

            default:
                Console.WriteLine($"Unknown command '{args[0]}'. Showing usage instructions:");
                DisplayUsage();
                break;
        }
    }

    // Constants for ChangeDisplaySettingsEx
    const int DISP_CHANGE_SUCCESSFUL = 0;
    const int DISP_CHANGE_RESTART = 1;
    const int DISP_CHANGE_FAILED = -1;
    const int DISP_CHANGE_BADMODE = -2;
    const int DISP_CHANGE_NOTUPDATED = -3;
    const int DISP_CHANGE_BADFLAGS = -4;
    const int DISP_CHANGE_BADPARAM = -5;

    // Additional flags for ChangeDisplaySettingsEx
    const int CDS_UPDATEREGISTRY = 0x01;
    const int CDS_TEST = 0x02;
    const int CDS_FULLSCREEN = 0x04;

    // Enum for current settings
    const int ENUM_CURRENT_SETTINGS = -1;
    const int ENUM_REGISTRY_SETTINGS = -2;

    // Fields for DEVMODE.dmFields
    const int DM_PELSWIDTH = 0x80000;
    const int DM_PELSHEIGHT = 0x100000;
    const int DM_DISPLAYFREQUENCY = 0x400000;
    const int DM_BITSPERPEL = 0x20000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;        // Color depth (bits per pixel)
        public uint dmPelsWidth;         // Screen width
        public uint dmPelsHeight;        // Screen height
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;  // Refresh rate
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }


    // Command Handlers
    private static void SetMonitorCommand(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Error: setmonitor requires a monitor name and client name (steamdecklcd, xbox, custom).");
            DisplayUsage();
            return;
        }

        string monitorName = args[1];
        string clientName = args[2].ToLower();

        var (instanceName, deviceID) = GetMonitorDeviceInfo(monitorName);

        if (string.IsNullOrEmpty(instanceName) || string.IsNullOrEmpty(deviceID))
        {
            Console.WriteLine($"Error: Could not find monitor {monitorName}");
            return;
        }

        if (clientName == "custom" && args.Length >= 6)
        {
            if (int.TryParse(args[3], out int customWidth) &&
                int.TryParse(args[4], out int customHeight) &&
                int.TryParse(args[5], out int customRefreshRate))
            {
                Console.WriteLine($"Applying custom settings to {deviceID}: {customWidth}x{customHeight}, {customRefreshRate}Hz");
                SetMonitor(instanceName, deviceID, clientName, customWidth, customHeight, customRefreshRate);
            }
            else
            {
                Console.WriteLine("Error: Invalid custom parameters. Please provide width, height, and refresh rate.");
                DisplayUsage();
            }
        }
        else
        {
            Console.WriteLine($"Applying {clientName} settings to {deviceID}...");
            SetMonitor(instanceName, deviceID, clientName);
        }
    }

    private static void ShowDeviceInfo(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: deviceinfo requires a monitor name.");
            DisplayUsage();
            return;
        }

        string monitorName = args[1];
        var (instanceName, deviceID) = GetMonitorDeviceInfo(monitorName);

        if (!string.IsNullOrEmpty(instanceName) && !string.IsNullOrEmpty(deviceID))
        {
            Console.WriteLine($"Found Monitor - InstanceName: {instanceName}, DeviceID: {deviceID}");
        }
        else
        {
            Console.WriteLine($"Error: Could not find monitor {monitorName}");
        }
    }

    private static void ListDisplayModesCommand(string[] args)
    {
        if (args.Length >= 2)
        {
            string monitorName = args[1];
            ListAvailableDisplayModes(monitorName);
        }
        else
        {
            Console.WriteLine("Error: Please specify the monitor name for listdisplaymodes.");
            DisplayUsage();
        }
    }

    // Monitor and Display Settings
    public static void SetMonitor(string instanceName, string deviceID, string clientName, int customWidth = 0, int customHeight = 0, int customRefreshRate = 0)
    {
        if (!Enum.TryParse(clientName, true, out ClientNameEnum client))
        {
            Console.WriteLine($"Error: Invalid client name '{clientName}' provided.");
            DisplayUsage();
            return;
        }

        var settings = new MonitorSettings
        {
            MonitorName = instanceName
        };

        ApplyClientSettings(client, settings, customWidth, customHeight, customRefreshRate);

        int result = ApplyMonitorSettings(settings);
        if (result != DISP_CHANGE_SUCCESSFUL)
        {
            Console.WriteLine("Failed to apply settings using API. Falling back to PowerShell...");
            ChangeMonitorResolutionWithPowerShell(instanceName, customWidth, customHeight, customRefreshRate);
        }
    }

    public static int ApplyMonitorSettings(MonitorSettings settings)
    {
        DEVMODE dm = new DEVMODE();
        dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

        // Set the fields for the DEVMODE structure
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_BITSPERPEL;
        dm.dmPelsWidth = (uint)settings.SetWidth;
        dm.dmPelsHeight = (uint)settings.SetHeight;
        dm.dmDisplayFrequency = (uint)settings.RefreshRate;
        dm.dmBitsPerPel = 32; // Assuming 32-bit color depth

        // Attempt to change resolution using ChangeDisplaySettingsEx
        int result = ChangeDisplaySettingsEx(settings.MonitorName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);

        // Return the result of the API call
        return result;
    }

    private static void ApplyClientSettings(ClientNameEnum client, MonitorSettings settings, int customWidth, int customHeight, int customRefreshRate)
    {
        switch (client)
        {
            case ClientNameEnum.Custom:
                if (customWidth <= 0 || customHeight <= 0 || customRefreshRate <= 0)
                {
                    Console.WriteLine("Error: You must provide valid custom width, height, and refresh rate for 'Custom' client.");
                    DisplayUsage();
                    return;
                }
                settings.SetWidth = customWidth;
                settings.SetHeight = customHeight;
                settings.RefreshRate = customRefreshRate;
                Console.WriteLine($"Custom settings applied to {settings.MonitorName}: {customWidth}x{customHeight}, {customRefreshRate}Hz");
                break;

            case ClientNameEnum.SteamDeckLCD:
                settings.SetWidth = 1280;
                settings.SetHeight = 800;
                settings.RefreshRate = 60;
                Console.WriteLine($"SteamDeckLCD settings applied to {settings.MonitorName}: {settings.SetWidth}x{settings.SetHeight}, {settings.RefreshRate}Hz");
                break;

            case ClientNameEnum.Xbox:
                settings.SetWidth = 2560;
                settings.SetHeight = 1440;
                settings.RefreshRate = 120;
                Console.WriteLine($"Xbox settings applied to {settings.MonitorName}: {settings.SetWidth}x{settings.SetHeight}, {settings.RefreshRate}Hz");
                break;

            default:
                Console.WriteLine("No valid client specified. Please select a valid client.");
                DisplayUsage();
                return;
        }
    }

    public static void ResetDisplaySettings()
    {
        DEVMODE dm = new DEVMODE();
        dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

        int resetResult = ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, 0, IntPtr.Zero);

        if (resetResult == DISP_CHANGE_SUCCESSFUL)
        {
            Console.WriteLine("Display settings reset to default.");
        }
        else
        {
            Console.WriteLine("Failed to reset display settings.");
        }
    }

    // Helper Methods
    public static (string instanceName, string deviceID) GetMonitorDeviceInfo(string monitorName)
    {
        string command = @"
            Get-WmiObject -Namespace root\wmi -Class WmiMonitorID | 
            ForEach-Object { 
                $instanceName = $_.InstanceName;
                $id = ($_.UserFriendlyName -notmatch 0 | ForEach-Object { [char]$_ }) -join '';
                [pscustomobject]@{ InstanceName = $instanceName; DeviceID = $id }
            }";

        string output = RunPowerShellScript(command);
        return ParseMonitorInfo(output, monitorName);
    }

    private static (string instanceName, string deviceID) ParseMonitorInfo(string output, string monitorName)
    {
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains(monitorName))
            {
                var columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length >= 2)
                {
                    return (columns[0], columns[1]);
                }
            }
        }

        Console.WriteLine($"No matching monitor found for: {monitorName}");
        return (null, null);
    }

    // External Calls and Interop
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    public static void ListMonitors()
    {
        try
        {
            using (var factory = new Factory1())
            {
                for (int adapterIndex = 0; ; adapterIndex++)
                {
                    if (!ListAdapterMonitors(factory, adapterIndex))
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing monitors: {ex.Message}");
        }
    }

    private static bool ListAdapterMonitors(Factory1 factory, int adapterIndex)
    {
        try
        {
            using (var adapter = factory.GetAdapter1(adapterIndex))
            {
                if (adapter == null) return false;

                var adapterDesc = adapter.Description1;
                Console.WriteLine($"====== ADAPTER {adapterIndex} =====");
                Console.WriteLine($"Device Name      : {adapterDesc.Description}");
                Console.WriteLine($"Device Vendor ID : 0x{adapterDesc.VendorId:X}");
                Console.WriteLine($"Device Device ID : 0x{adapterDesc.DeviceId:X}");
                Console.WriteLine($"Device Video Mem : {adapterDesc.DedicatedVideoMemory / 1048576} MiB");
                Console.WriteLine($"Device Sys Mem   : {adapterDesc.DedicatedSystemMemory / 1048576} MiB");
                Console.WriteLine($"Shared Sys Mem   : {adapterDesc.SharedSystemMemory / 1048576} MiB");
                Console.WriteLine();

                // Enumerate outputs (monitors) for this adapter
                for (int outputIndex = 0; ; outputIndex++)
                {
                    if (!ListMonitorOutputs(adapter, outputIndex))
                        break;
                }
            }
        }
        catch (SharpDXException ex)
        {
            if (ex.ResultCode == ResultCode.NotFound)
                return false; // No more adapters to enumerate
            else
                throw; // Rethrow for any other error
        }
        return true;
    }

    private static bool ListMonitorOutputs(Adapter1 adapter, int outputIndex)
    {
        try
        {
            using (var output = adapter.GetOutput(outputIndex))
            {
                if (output == null) return false;

                var outputDesc = output.Description;
                var width = outputDesc.DesktopBounds.Right - outputDesc.DesktopBounds.Left;
                var height = outputDesc.DesktopBounds.Bottom - outputDesc.DesktopBounds.Top;

                Console.WriteLine("    ====== OUTPUT ======");
                Console.WriteLine($"    Output Name       : {outputDesc.DeviceName}");
                Console.WriteLine($"    AttachedToDesktop : {(outputDesc.IsAttachedToDesktop ? "yes" : "no")}");
                Console.WriteLine($"    Resolution        : {width}x{height}");
                Console.WriteLine();
            }
        }
        catch (SharpDXException ex)
        {
            if (ex.ResultCode == ResultCode.NotFound)
                return false; // No more outputs to enumerate
            else
                throw; // Rethrow for any other error
        }
        return true;
    }

    // PowerShell Command Execution and Utility Methods
    public static string RunPowerShellScript(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi))
        {
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine("PowerShell Error: " + error);
            }

            return output;
        }
    }

    public static void ChangeMonitorResolutionWithPowerShell(string instanceName, int width, int height, int refreshRate)
    {
        // Properly construct the PowerShell command to avoid the compilation error
        string command = $@"
$display = Get-WmiObject -Namespace root\wmi -Class WmiMonitorBasicDisplayParams | 
    Where-Object {{ $_.InstanceName -like '*{instanceName}*' }};
if ($display) {{
    Add-Type -TypeDefinition @'
    using System;
    using System.Runtime.InteropServices;
    public class NativeMethods {{
        [DllImport(""user32.dll"", CharSet = CharSet.Auto)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr devMode, IntPtr hwnd, uint dwflags, IntPtr lParam);
    }}
'@;
    $result = [NativeMethods]::ChangeDisplaySettingsEx('{instanceName}', [IntPtr]::Zero, [IntPtr]::Zero, 0, [IntPtr]::Zero);
    if ($result -eq 0) {{
        Write-Host 'Resolution changed successfully.';
    }} else {{
        Write-Host 'Failed to change resolution. Error Code: ' + $result;
    }}
}} else {{
    Write-Host 'Monitor not found.';
}}";

        // Run the PowerShell script
        RunPowerShellScript(command);
    }

    public static void ListAvailableDisplayModes(string monitorName)
    {
        DEVMODE dm = new DEVMODE();
        dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

        Console.WriteLine($"Available display modes for {monitorName}:");
        for (int i = 0; EnumDisplaySettings(monitorName, i, ref dm); i++)
        {
            Console.WriteLine($"Mode {i}: {dm.dmPelsWidth}x{dm.dmPelsHeight}, {dm.dmDisplayFrequency}Hz");
        }
    }

    public static void DisplayUsage()
    {
        Console.WriteLine("Usage: ScreenConfig.exe [setmonitor] [\"monitorname\"] [ClientName] [CustomWidth] [CustomHeight] [CustomRefreshRate]");
        Console.WriteLine();
        Console.WriteLine("Parameters:");
        Console.WriteLine("  setmonitor: Command to set monitor settings.");
        Console.WriteLine(@"  'monitorname': The name of the monitor (e.g., '\\.\DISPLAY1', 'LG ULTRAGEAR'). Monitor names with spaces must be in quotes.");
        Console.WriteLine("  ClientName: A string value to specify the target device (steamdecklcd, xbox, custom).");
        Console.WriteLine("    Example: steamdecklcd, xbox, custom");
        Console.WriteLine("    If you select 'custom', you must provide the custom resolution, refresh rate.");
        Console.WriteLine("  CustomWidth (optional): Integer specifying the screen width (required for 'custom').");
        Console.WriteLine("    Example: 1920");
        Console.WriteLine("  CustomHeight (optional): Integer specifying the screen height (required for 'custom').");
        Console.WriteLine("    Example: 1080");
        Console.WriteLine("  CustomRefreshRate (optional): Integer specifying the screen refresh rate in Hz (required for 'custom').");
        Console.WriteLine("    Example: 60");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine(@"  ScreenConfig.exe setmonitor ""LG ULTRAGEAR"" custom 1920 1080 60");
        Console.WriteLine("    Applies custom resolution 1920x1080, 60Hz to 'LG ULTRAGEAR'.");
        Console.WriteLine();
        Console.WriteLine("  ScreenConfig.exe resetmonitor");
        Console.WriteLine("    Resets the display settings to default.");
        Console.WriteLine();
    }
}