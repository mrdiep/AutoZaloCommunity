﻿namespace ZaloCommunityDev.Shared.Structures
{
    public struct FriendPositionMessage
    {
        public string Name { get; set; }
        public ScreenPoint Point { get; set; }

        public static bool IsEmpty(FriendPositionMessage profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.Name))
                return false;

            return true;
        }
    }
}

