using GreatLakesCharts;

return ChartGenerator.Run(new LakeConfig(
    "ontario_with_average.csv",
    "Ontario",
    "Lake Ontario Ice Coverage",
    "Couverture des glaces du lac Ontario"), args);
