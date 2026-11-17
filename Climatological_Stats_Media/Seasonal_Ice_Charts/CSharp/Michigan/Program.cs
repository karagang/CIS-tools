using GreatLakesCharts;

return ChartGenerator.Run(new LakeConfig(
    "michigan_with_average.csv",
    "Michigan",
    "Lake Michigan Ice Coverage",
    "Couverture des glaces du lac Michigan"), args);
