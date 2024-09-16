using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dxgi_info_Csharp.models
{
    public class SteamDeckSettings
    {
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 800;
        public int RefreshRate { get; set; } = 60;
        public bool EnableHDR { get; set; } = false;
    }
}
