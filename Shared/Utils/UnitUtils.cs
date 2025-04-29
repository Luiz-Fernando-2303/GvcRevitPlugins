namespace GvcRevitPlugins.Shared.Utils
{
    internal static class UnitUtils
    {
        public static double MeterToFeet(double value) => value * 3.28084;
        public static double FeetToMeter(double value) => value / 3.28084;
    }
}
