﻿using GalaSoft.MvvmLight;
using ZaloCommunityDev.Models;
using ZaloCommunityDev.Shared;

namespace ZaloCommunityDev.ViewModel
{
    public class SettingViewModel : ViewModelBase
    {
        public string AndroidDebugBridgeOsLocation { get; set; } = @"C:\Program Files\Leapdroid\VM";

        public ScreenInfo Screen { get; } = new ScreenInfo();

        public Delay Delay { get; set; } = new Delay();

        public AddFriendNearByConfig AddingFriendConfig { get; set; }

        public string TextAddFriend { get; set; } = "Hi. ";
        public int MaxFriendAddedPerDay { get; set; } = 23;
        public int FriendAddedToDay { get; set; } = 0;
    }
}