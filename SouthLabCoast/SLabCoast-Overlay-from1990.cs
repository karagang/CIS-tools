using System;
using EastCoastIce;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        return OverlayCharts.Run(args, "weekly-SLab-Coast-1970-2026.csv", "South Labrador Coast", "de la côte sud du Labrador", true);
    }
}
