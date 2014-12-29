﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hurricane.AppMainWindow.WindowSkins
{
    public class WindowSkinConfiguration
    {
        public bool ShowTitleBar { get; set; }
        public bool ShowSystemMenuOnRightClick { get; set; }
        public double MinWidth { get; set; }
        public double MinHeight { get; set; }
        public double MaxWidth { get; set; }
        public double MaxHeight { get; set; }

        public bool ShowWindowControls { get; set; }
        public bool NeedMovingHelp { get; set; }
        public bool ShowFullscreenDialogs { get; set; }
    }
}
