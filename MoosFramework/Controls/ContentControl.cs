﻿using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace Moos.Framework.Controls
{
    public abstract partial class ContentControl : Layout, IView
    {
        public string Name { get; set; }

        public IntPtr Handler { get; internal set; }
        public Window OwnerWindow { set; get; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Thickness Margin { get; set; }
        public LayoutOptions HorizontalOptions { set; get; }
        public LayoutOptions VerticalOptions { set; get; }
        public Color Background { get; set; }
        public bool IsVisisble { get; set; }
        internal int GridRow { get; set; }
        internal int GridColumn { get; set; }

        public ContentControl()
        {
            IsVisisble = true;
        }

        public virtual void OnLoaded()
        {

        }
    }
}
