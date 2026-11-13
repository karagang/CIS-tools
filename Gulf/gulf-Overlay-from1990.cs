using System;
using EastCoastIce;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        return OverlayCharts.Run(args, "weeklyGulf-seasons.csv", "Gulf of St. Lawrence", "du golfe du Saint-Laurent", true);
    }
}
