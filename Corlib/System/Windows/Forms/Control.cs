using System.Drawing;

namespace System.Windows.Forms
{
    public class Control
    {
        public static Point MousePosition;
        public static Point MouseOffSet = new Point();
        public static MouseButtons MouseButtons;
        static MouseButtons lastMouseButtons;
        public static bool Clicked { private set; get; }

        public static void Update()
        {
            if (lastMouseButtons == MouseButtons.None && MouseButtons == MouseButtons.Left)
            {
                Clicked = true;
            }
            else
            {
                Clicked = false;
            }
            lastMouseButtons = MouseButtons;
        }
    }
}
