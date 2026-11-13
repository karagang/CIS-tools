using System;
using EastCoastIce;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        return OverlayCharts.Run(args, "weekly_NE-NFLND.csv", "NE Newfoundland", "du nord-est de Terre-Neuve", true);
    }
}
