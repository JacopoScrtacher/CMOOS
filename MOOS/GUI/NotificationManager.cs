﻿#if HasGUI
using MOOS.Driver;
using MOOS.Misc;
using MOOS.NET;
using MOOS.NET.Config;
using System;
using System.Collections.Generic;
using System.Windows;

namespace MOOS.GUI
{
    public enum NotificationLevel
    {
        None,
        Error
    }

    public class Nofity
    {
        public int X, Y;
        public readonly string Message;
        public NotificationLevel NotificationLevel;
        public int SWidth;
        public int SHeight;

        public ulong DisposeUntil;
        public Animation ani;

        public Nofity(string msg, NotificationLevel level = NotificationLevel.None)
        {
            DisposeUntil = 0;
            Message = msg;
            X = 0; Y = 0;
            SWidth = WindowManager.font.MeasureString(msg);
            SHeight = WindowManager.font.FontSize;
            NotificationLevel = level;

            ani = new Animation()
            {
                MaximumValue = NotificationManager.Threshold + SWidth,
                Stopped = true,
            };
            Animator.AddAnimation(ani);
        }

        public override void Dispose()
        {
            Message.Dispose();
            Animator.DisposeAnimation(ani);
            base.Dispose();
        }
    }

    public static class NotificationManager
    {
        static List<Nofity> Notifications;

        public static unsafe void Initialize()
        {
            Notifications = new();
            
            Add(new Nofity("Welcome to MOOS"));
            Add(new Nofity(Audio.HasAudioDevice ? "Info: Audio controller available" : "Warn: No audio controller found on this PC", Audio.HasAudioDevice ? NotificationLevel.None : NotificationLevel.Error));
            
            if (NetworkDevice.Devices.Count > 0 && NetworkConfiguration.Count > 0)
            {
                Add(new Nofity($"Info: Network device {NetworkDevice.Devices[0].NameID} ({NetworkConfiguration.CurrentAddress.ToString()})", NotificationLevel.None));
            }

            if (VMwareTools.Available)
            {
                Add(new Nofity("VMware Tools is working", NotificationLevel.None));
            }
            
        }

        public static void Add(Nofity nofity)
        {
            Notifications.Add(nofity);
        }

        public const int Devide = 30;

        public const int Threshold = 50;

        public const int DisposeUntil = 1000;

        public static void Draw()
        {
            for (int i = 0; i < Notifications.Count; i++)
            {
                var v = Notifications[i];
                if (v.X < (Threshold + v.SWidth))
                {
                    v.ani.Stopped = false;
                    v.X = v.ani.Value;
                    break;
                }
            }

            for (int i = 0; i < Notifications.Count; i++)
            {
                var v = Notifications[i];

                if (v.X < (Threshold + v.SWidth))
                {
                    break;
                }
                else
                {
                    if (v.DisposeUntil == 0)
                    {
                        v.DisposeUntil = Timer.Ticks + DisposeUntil;
                    }
                    if (Timer.Ticks > v.DisposeUntil)
                    {
                        Notifications.Remove(v);
                        v.Dispose();
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            int y = Devide * 2;

            for (int i = 0; i < Notifications.Count; i++)
            {
                var v = Notifications[i];

                Framebuffer.Graphics.FillRectangle(Framebuffer.Width - v.X, v.Y + y, v.SWidth + Devide, v.SHeight + Devide, 0xFF111111);
                Framebuffer.Graphics.DrawRectangle(Framebuffer.Width - v.X, v.Y + y, v.SWidth + Devide, v.SHeight + Devide, 0xFF222222);
                Framebuffer.Graphics.FillRectangle(Framebuffer.Width - v.X, v.Y + y, 5, v.SHeight + Devide, v.NotificationLevel == NotificationLevel.None ? 0xFF80B000 : 0xFFE74C3C);
                WindowManager.font.DrawString(Framebuffer.Width - v.X + (Devide / 2), v.Y + y + (Devide / 2), v.Message, 0xFFFFFFFF);

                y += v.SHeight + Devide;
                y += Devide;
            }
        }
    }
}
#endif