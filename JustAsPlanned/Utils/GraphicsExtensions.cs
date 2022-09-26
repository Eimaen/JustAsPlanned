using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustAsPlanned.Utils
{
    public static class GraphicsExtensions
    {
        public static void DrawCenteredImage(this Graphics g, Image image, Point center, float scaleFactor)
        {
            Point absolutePosition = new Point((int)(center.X - image.Width * scaleFactor / 2), (int)(center.Y - image.Height * scaleFactor / 2));
            g.DrawImage(image, absolutePosition.X, absolutePosition.Y, image.Width * scaleFactor, image.Height * scaleFactor);
        }
    }
}
