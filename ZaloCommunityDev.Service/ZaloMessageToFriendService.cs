﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using ZaloCommunityDev.Data;
using ZaloCommunityDev.ImageProcessing;
using ZaloCommunityDev.Shared;
using ZaloCommunityDev.Shared.Structures;
using System;
using ZaloCommunityDev.Service.Models;

namespace ZaloCommunityDev.Service
{
    public class ZaloMessageService : ZaloCommunityDistributeServiceBase
    {
        private readonly ILog _log = LogManager.GetLogger(nameof(ZaloMessageService));

        public ZaloMessageService(Settings settings, DatabaseContext dbContext, IZaloImageProcessing zaloImageProcessing, ZaloAdbRequest zaloAdbRequest)
            : base(settings, dbContext, zaloImageProcessing, zaloAdbRequest)
        {
        }

        public void SendMessageToFriendInContactList(Filter filter)
        {
            try
            {
                var countSuccess = 0;

                GotoPage(Activity.MainTab);

                Delay(1000);

                TouchAt(Screen.HomeScreenFriendTab);

                if (!string.IsNullOrWhiteSpace(filter.IncludedPeopleNames))
                {
                    SendMessageToFriendWithNames(filter);

                    return;
                }

                Delay(1000);
                ZaloHelper.Output("Đang phân tích dữ liệu");

                var fileCapture = CaptureScreenNow();
                var friends = ZaloImageProcessing.GetFriendProfileList(fileCapture, Screen);

                ZaloHelper.OutputLine();
                friends.ToList().ForEach(x => ZaloHelper.Output(x.Name));
                ZaloHelper.OutputLine();

                var stack = new Stack<FriendPositionMessage>(friends.Where(x => !string.IsNullOrWhiteSpace(x.Name)).OrderByDescending(x => x.Point.Y));
                var profilesPage1 = stack.Select(x => x.Name).ToArray();

                while (countSuccess <= filter.NumberOfAction)
                {
                    while (stack.Count == 0)
                    {
                        ScrollList(9);

                        ZaloHelper.Output("Đang phân tích dữ liệu màn hình");
                        fileCapture = CaptureScreenNow();
                        friends = ZaloImageProcessing.GetFriendProfileList(fileCapture, Screen);

                        ZaloHelper.OutputLine();
                        friends.ToList().ForEach(x => ZaloHelper.Output(x.Name));
                        ZaloHelper.OutputLine();

                        stack = new Stack<FriendPositionMessage>(friends.OrderByDescending(x => x.Point.Y));
                        var profilesPage2 = stack.Select(x => x.Name).ToArray();
                        if (!profilesPage2.Except(profilesPage1).Any())
                        {
                            ZaloHelper.Output("Hết danh sách");
                            return;
                        }

                        profilesPage1 = profilesPage2;
                    }

                    Delay(2000);

                    var rowFriend = stack.Pop();

                    if (DbContext.LogMessageSentToFriendSet.FirstOrDefault(x => x.Name == rowFriend.Name && x.Account == Settings.User.Username) != null)
                    {
                        ZaloHelper.Output($"Đã gửi tin cho bạn {rowFriend.Name} rồi");

                        continue;
                    }

                    var profile = DbContext.ProfileSet.FirstOrDefault(x => x.Name == rowFriend.Name);
                    var request = new ChatRequest
                    {
                        Profile = new ProfileMessage
                        {
                            Name = rowFriend.Name,
                            Location = profile?.Location,
                            PhoneNumber = profile?.PhoneNumber
                        },
                        Objective = ChatObjective.FriendInContactList
                    };

                    if (Screen.InfoRect.Contains(rowFriend.Point))
                    {
                        TouchAt(rowFriend.Point);
                        Delay(2000);

                        NavigateToProfileScreenFromChatScreenToGetInfoThenGoBack(request);

                        string reason;
                        if (!filter.IsValidProfile(request.Profile, out reason))
                        {
                            ZaloHelper.Output("Bỏ qua bạn này, lý do: " + reason);
                            TouchAtIconTopLeft(); //Touch to close side bar
                            Delay(400);
                        }
                        else
                        {
                            if (Chat(request, filter))
                            {
                                countSuccess++;
                                if (profile == null)
                                {
                                    DbContext.AddProfile(request.Profile, Settings.User.Username);
                                }
                            }

                            TouchAtIconTopLeft();//GO BACK PROFILE
                        }
                    }
                }
            }
            catch (Exception ex) { _log.Error(ex); }
            finally
            {
                ZaloHelper.SendCompletedTaskSignal();
            }
        }

        private void SendMessageToFriendWithNames(Filter filter)
        {
            var stack = new Stack<string>(filter.IncludedPeopleNames.ZaloSplitText());
            while (stack.Count > 0)
            {
                Delay(1000);
                TouchAtIconBottomLeft();//Open search
                Delay(500);
                var name = stack.Pop();
                
                TouchAt(Screen.HomeScreenFriendTabSearchTextField);
                Delay(500);

                SendText(name);

                var rowFriends = ZaloImageProcessing.GetFriendProfileList(CaptureScreenNow(), Screen);
                if (!rowFriends.Any())
                {
                    ZaloHelper.Output("Không có kết quả");

                    TouchAtIconTopLeft();

                    continue;
                }

                TouchAt(Screen.HomeScreenFriendTabSearchFristItem);
                Delay(500);

                var request = new ChatRequest() { Objective = ChatObjective.FriendInContactList, Profile = new ProfileMessage() };
                NavigateToProfileScreenFromChatScreenToGetInfoThenGoBack(request);

                string reason;
                if (!filter.IsValidProfile(request.Profile, out reason))
                {
                    ZaloHelper.Output("Bỏ qua bạn này, lý do: " + reason);
                    TouchAtIconTopLeft(); //Touch to close side bar
                    Delay(400);
                }
                else
                {
                    Chat(request, filter);

                    TouchAtIconTopLeft();//GO BACK PROFILE
                }
            }

            //Search in contact
        }

        public void SendMessageByPhoneNumber(Filter filter)
        {
            try
            {
                var canSentToday = Settings.MaxMessageStrangerPerDay - DbContext.GetMessageToStragerCount(Settings.User.Username);
                var numberOfAction = filter.NumberOfAction > canSentToday ? canSentToday : filter.NumberOfAction;
                if (numberOfAction <= 0)
                {
                    ZaloHelper.Output("đã gửi hết số bạn trong ngày rồi");

                    return;
                }

                var phonelist = filter.IncludePhoneNumbers.Split(";,|".ToArray()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

                var countSuccess = 0;
                var stack = new Stack<string>(phonelist);

                while (countSuccess < numberOfAction)
                {
                    GotoPage(Activity.FindFriendByPhoneNumber);
                    var success = false;

                    while (!success)
                    {
                        if (stack.Count == 0)
                        {
                            return;
                        }
                        var phoneNumber = stack.Pop();
                        ZaloHelper.Output($"Tiến hành gửi tin qua số đt {phoneNumber}");
                        if (DbContext.LogMessageSentToStrangerSet.FirstOrDefault(x => x.PhoneNumber == phoneNumber && x.Account == Settings.User.Username) != null)
                        {
                            ZaloHelper.Output($"Đã gửi tin cho số đt '{phoneNumber}' rồi");

                            continue;
                        }

                        Thread.Sleep(100);

                        DeleteWordInFocusedTextField();
                        SendText(phoneNumber);

                        SendKey(KeyCode.AkeycodeEnter);
                        Thread.Sleep(4000);

                        ZaloHelper.Output("!đang kiểm tra số điện thoại khả dụng");
                        if (ZaloImageProcessing.HasFindButton(CaptureScreenNow(), Screen))
                        {
                            ZaloHelper.Output("!Lỗi, số đt không có");
                        }
                        else
                        {


                            var profile = GrabProfileInfo();
                            profile.PhoneNumber = phoneNumber;

                            string reason;
                            if (!filter.IsValidProfile(profile, out reason))
                            {
                                ZaloHelper.Output("Bỏ qua bạn này, lý do: " + reason);

                                TouchAtIconTopLeft(); //Go back to phone Entry
                                Delay(400);
                            }
                            else
                            {
                                var request = new ChatRequest { Profile = profile, Objective = ChatObjective.StrangerByPhone };

                                TouchAt(Screen.IconBottomLeft);
                                Delay(800);

                                if (Chat(request, filter))
                                {
                                    countSuccess++;
                                    DbContext.AddProfile(request.Profile, Settings.User.Username);

                                    success = true;
                                }

                                TouchAtIconTopLeft();//GO BACK FRIENDLIST
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
            finally
            {
                ZaloHelper.SendCompletedTaskSignal();
            }
        }

        public void SendMessageNearBy(Filter filter)
        {
            filter.Locations = SetLocationByName(filter.Locations);

            try
            {
                var canSentToday = Settings.MaxMessageStrangerPerDay - DbContext.GetMessageToStragerCount(Settings.User.Username);
                var numberOfAction = filter.NumberOfAction > canSentToday ? canSentToday : filter.NumberOfAction;
                if (numberOfAction <= 0)
                {
                    ZaloHelper.Output("đã gửi hết số bạn trong ngày rồi");

                    return;
                }

                var gender = filter.GenderSelection;
                var ageValues = filter.FilterAgeRange?.Split("-".ToArray());

                string ageFrom = "18";
                string ageTo = "50";
                if (ageValues.Length == 2)
                {
                    ageFrom = ageValues[0];
                    ageTo = ageValues[1];
                }

                GotoPage(Activity.UserNearbyList);

                AddSettingSearchFriend(gender, ageFrom, ageTo);

                ChatFriendNearBy(numberOfAction, filter);
            }
            catch (Exception ex) { _log.Error(ex); }
            finally
            {
                ZaloHelper.SendCompletedTaskSignal();
            }
        }

        private FriendPositionMessage[] GetPositionAccountNotSent(Action<string[]> allPrrofiles)
        {
            var captureFiles = CaptureScreenNow();
            var names = ZaloImageProcessing.GetListFriendName(captureFiles, Screen);

            var t = names.Where(v => DbContext.LogMessageSentToFriendSet.FirstOrDefault(x => x.Name == v.Name && x.Account == Settings.User.Username) == null).ToArray();
            var t2 = t.Where(v => DbContext.LogMessageSentToStrangerSet.FirstOrDefault(x => x.Name == v.Name && x.Account == Settings.User.Username) == null).ToArray();

            allPrrofiles(names.Select(x => x.Name).ToArray());

            return t2.ToArray();
        }

        private void ChatFriendNearBy(int maxFriendToday, Filter filter)
        {
            ZaloHelper.Output($"!bắt đầu gửi tin cho bạn gần đây. Số bạn yêu cầu tối đa trong ngày hôm nay là {maxFriendToday}");
            var countSuccess = 0;
            string[] profilesPage1 = null;
            string[] profilesPage2 = null;
            ZaloHelper.Output("!đang tìm thông tin các bạn");
            var friendNotAdded = (GetPositionAccountNotSent(x => profilesPage1 = x)).OrderByDescending(x => x.Point.Y);
            var points = new Stack<FriendPositionMessage>(friendNotAdded);

            profilesPage1.ToList().ForEach(x => ZaloHelper.Output($"!tìm thấy bạn trên màn hình: {x}"));
            ZaloHelper.Output("!--------------------");
            friendNotAdded.ToList().ForEach(x => ZaloHelper.Output($"!các bạn chưa được gửi lời mời: {x.Name}"));
            while (countSuccess < maxFriendToday)
            {
                while (points.Count == 0)
                {
                    ZaloHelper.Output("!đang cuộn danh sách bạn");
                    ScrollList(9);

                    ZaloHelper.Output("!đang tìm thông tin các bạn");

                    friendNotAdded = GetPositionAccountNotSent(x => profilesPage2 = x).OrderByDescending(x => x.Point.Y);
                    points = new Stack<FriendPositionMessage>(friendNotAdded);
                    profilesPage2.ToList().ForEach(x => ZaloHelper.Output($"!tìm thấy bạn trên màn hình: {x}"));

                    ZaloHelper.OutputLine();
                    friendNotAdded.ToList().ForEach(x => ZaloHelper.Output($"!bạn chưa được gửi tin nhắn: {x}"));
                    ZaloHelper.OutputLine();

                    if (!profilesPage2.Except(profilesPage1).Any())
                    {
                        ZaloHelper.Output("!hết bạn trong danh sách.");

                        return;
                    }

                    profilesPage1 = profilesPage2;
                }

                Delay(2000);

                var pointRowFriend = points.Pop();

                var request = new ChatRequest
                {
                    Profile = new ProfileMessage
                    {
                        Name = pointRowFriend.Name,
                        Location = filter.Locations
                    },
                    Objective = ChatObjective.StrangerNearBy
                };

                if (Screen.InfoRect.Contains(pointRowFriend.Point))
                {
                    TouchAt(pointRowFriend.Point);
                    Delay(2000);//wait to navigate chat screen

                    var infoGrab = GrabProfileInfo(pointRowFriend.Name);
                    var profile = request.Profile;
                    ZaloHelper.CopyProfile(ref profile, infoGrab);

                    string reason;
                    if (!filter.IsValidProfile(request.Profile, out reason))
                    {
                        ZaloHelper.Output("Bỏ qua bạn này, lý do: " + reason);
                        TouchAt(Screen.IconTopLeft);
                        Delay(300);
                    }
                    else
                    {
                        TouchAt(Screen.IconBottomLeft);
                        Delay(800);

                        if (Chat(request, filter))
                        {
                            DbContext.AddProfile(request.Profile, Settings.User.Username);
                            countSuccess++;
                            ZaloHelper.Output($"!gửi tin nhắn tới: {request.Profile.Name} thành công. Số bạn đã gửi thành công trong phiên này là: {countSuccess}");

                            TouchAtIconTopLeft();//Go Back TO PROFILE

                            TouchAtIconTopLeft();// GO BACK TO FRIENDLIST
                        }
                    }
                }
            }
        }

        public void NavigateToProfileScreenFromChatScreenToGetInfoThenGoBack(ChatRequest request)
        {
            //GrabInfomation
            TouchAtIconTopRight();
            Delay(1000);
            TouchAt(Screen.ChatScreenProfileAvartar);
            Delay(2000);

            var infoGrab = GrabProfileInfo(request.Profile.Name);

            var profileCopy = request.Profile;
            ZaloHelper.CopyProfile(ref profileCopy, infoGrab);

            TouchAtIconTopLeft();//Back to chat screen
            TouchAtIconTopLeft();//Close sidebar
            //End friend
        }

        private bool Chat(ChatRequest profile, Filter filter)
        {

            TouchAt(Screen.ChatScreenTextField);
            Delay(300);

            var messages = ZaloHelper.GetZalomessages(profile.Profile, filter);
            foreach (var message in messages)
            {
                DeleteWordInFocusedTextField(20);

                if (message.Type == ZaloMessageType.Text)
                {
                    SendText(message.Value);
                    Delay(500);

                    if (!IsDebug)
                    {
                        //TouchAt(Screen.ChatScreenSendButton);
                        SendKey(KeyCode.AkeycodeEnter);
                    }

                    ZaloHelper.Output($"Gửi tin nhắn chữ '{message.Value}' tới bạn '{profile.Profile.Name}' thành công");
                }
                else
                {
                    UpImageChat(new System.IO.FileInfo(message.Value));

                    Delay(500);
                    TouchAt(Screen.ChatScreenOpenMoreWindowButton);

                    Delay(500);
                    TouchAt(Screen.ChatScreenAddImageButton);

                    Delay(500);
                    TouchAt(Screen.UploadAlbumDCimFolter);

                    Delay(500);
                    TouchAt(Screen.UploadAlbumFirstImageCheckBox);

                    if (!Settings.IsDebug)
                    {
                        Delay(500);
                        TouchAt(Screen.UploadAlbumSendButton);
                    }
                    else
                    {
                        Delay(500);
                        TouchAtIconTopLeft();

                        Delay(500);
                        TouchAtIconTopLeft();
                    }
                    ZaloHelper.Output($"Gửi tin nhắn hình '{message.Value}' tới bạn '{profile.Profile.Name}' thành công");

                    Delay(900);
                    TouchAt(Screen.ChatScreenCloseMoreWindowButton);

                    Delay(100);
                }

                Delay(1000);

                switch (profile.Objective)
                {
                    case ChatObjective.FriendInContactList:
                        DbContext.AddLogMessageSentToFriend(profile.Profile, Settings.User.Username, message.Value);

                        break;

                    case ChatObjective.StrangerByPhone:
                    case ChatObjective.StrangerNearBy:
                        DbContext.AddLogMessageSentToStranger(profile.Profile, Settings.User.Username, message.Value);

                        break;
                }
            }

            return true;
        }
    }
}