using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dxgi_info_Csharp.models
{
    public class MonitorSettings
    {
        public string MonitorName { get; set; } = @"\\.\DISPLAY1";
        public int SetWidth { get; set; } = 3440;
        public int SetHeight { get; set; } = 1440;
        public int RefreshRate { get; set; } = 160;
        public bool EnableHDR { get; set; } = false;
        public ClientNameEnum Client {  get; set; }

    }
    public enum ClientNameEnum
    {
        None = 0,
        SteamDeckLCD = 1,
        Xbox = 2,
        Custom = 10,
    }
}
