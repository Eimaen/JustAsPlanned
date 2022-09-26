using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustAsPlanned.Utils
{
    public static class ColorManager
    {
        public static Color FromHSV(double h, double s, double v)
        {
            if (s == 0)
                return Color.FromArgb(255, (int)v, (int)v, (int)v);

            double max = v < 0.5d ? v * (1 + s) : (v + s) - (v * s);
            double min = (v * 2d) - max;

            return Color.FromArgb(255, (int)(255 * RGBChannelFromHue(min, max, h / 360d + 1 / 3d)), (int)(255 * RGBChannelFromHue(min, max, h / 360d)), (int)(255 * RGBChannelFromHue(min, max, h / 360d - 1 / 3d)));
        }

        private static double RGBChannelFromHue(double m1, double m2, double h)
        {
            h = (h + 1d) % 1d;
            if (h < 0) h += 1;
            if (h * 6 < 1)
                return m1 + (m2 - m1) * 6 * h;
            else if (h * 2 < 1)
                return m2;
            else if (h * 3 < 2)
                return m1 + (m2 - m1) * 6 * (2d / 3d - h);
            else
                return m1;
        }
    }
}
