﻿using System;
using log4net;
using ZaloCommunityDev.Data;
using ZaloCommunityDev.ImageProcessing;
using ZaloCommunityDev.Shared;
using ZaloCommunityDev.Service.Models;

namespace ZaloCommunityDev.Service
{
    public class ZaloLoginService : ZaloCommunityDistributeServiceBase
    {
        private readonly ILog _log = LogManager.GetLogger(nameof(ZaloLoginService));

        public ZaloLoginService(Settings settings, DatabaseContext dbContext, IZaloImageProcessing zaloImageProcessing, ZaloAdbRequest zaloAdbRequest)
            : base(settings, dbContext, zaloImageProcessing, zaloAdbRequest)
        {
        }

        public void Login(User user)
        {
            try
            {
                int count = 0;
                bool finish = false;
                while (!finish && count < 5)
                {
                    //EnableAbdKeyoard();
                    var account = user.Username;
                    var password = user.Password;
                    var region = user.Region;

                    ZaloHelper.Output("Đóng quá trình trước: " + account);
                    InvokeProc("/c adb shell am force-stop com.zing.zalo");
                    Delay(Settings.Delay.WaitForceCloseApp);

                    ZaloHelper.Output("Tiến hành đăng nhập vào tài khoản: " + account);
                    GotoPage(Activity.LoginUsingPw);

                    Delay(Settings.Delay.WaitLoginScreenOpened);

                    ZaloHelper.Output("Chọn khu vực trong cửa sổ mới" + region);
                    TouchAt(Screen.LoginScreenCountryCombobox);
                    Delay(1000);
                    TouchAt(Screen.IconTopRight);
                    Delay(1100);
                    SendText(region);
                    Delay(1200);
                    TouchAt(Screen.LoginScreenFirstCountryItem);
                    Delay(500);

                    ZaloHelper.Output("đang gửi tên đăng nhập");
                    TouchAt(Screen.LoginScreenPhoneTextField);
                    Delay(200);
                    TouchAt(Screen.LoginScreenPhoneTextField);
                    Delay(200);
                    for (var i = 0; i < 12; i++)
                    {
                        SendKey(KeyCode.AkeycodeDel);
                    }
                    SendText(account);

                    ZaloHelper.Output("đang gửi mật khẩu");
                    Delay(100);
                    TouchAt(Screen.LoginScreenPasswordTextField);
                    Delay(500);
                    SendText(password);
                    TouchAt(Screen.LoginScreenOkButton);
                    //SendKey(KeyCode.AkeycodeEnter, 2);

                    Delay(Settings.Delay.WaitLogin);
                    ZaloHelper.Output("đang gửi thông tin đăng nhập");

                    if (ZaloImageProcessing.HasLoginButton(CaptureScreenNow(), Settings.Screen))
                    {
                        ZaloHelper.Output("đăng nhập thất bại, đang thử lại");
                    }
                    else
                    {
                        finish = true;
                    }
                    count++;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                ZaloHelper.Output("Có lỗi xảy ra");
            }
        }
    }
}