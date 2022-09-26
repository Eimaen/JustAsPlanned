using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustAsPlanned.Utils
{
    public static class ColorExtensions
    {
        public static float GetActualBrightness(this Color color) => (color.R * 0.299f + color.G * 0.587f + color.B * 0.114f) / 256f;
        public static Color SetHue(this Color color, double hue) => ColorManager.FromHSV(hue, color.GetSaturation(), color.GetActualBrightness());
        public static Color SetBrightness(this Color color, double brightness) => ColorManager.FromHSV(color.GetHue(), color.GetSaturation(), brightness);
    }
}
