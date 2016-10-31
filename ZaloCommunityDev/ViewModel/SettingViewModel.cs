﻿using GalaSoft.MvvmLight;
using ZaloCommunityDev.Shared;

namespace ZaloCommunityDev.ViewModel
{
    public class SettingViewModel : ViewModelBase
    {
        public string AndroidDebugBridgeOsLocation { get; set; } = @"C:\Program Files\Leapdroid\VM";

        public ScreenInfo Screen { get; } = new ScreenInfo();

        public Delay Delay { get; set; } = new Delay();

        public int MaxFriendAddedPerDay { get; set; } = 30;
    }
}