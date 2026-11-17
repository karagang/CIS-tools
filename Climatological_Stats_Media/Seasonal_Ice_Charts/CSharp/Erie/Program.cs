using GreatLakesCharts;

return ChartGenerator.Run(new LakeConfig(
    "erie_with_average.csv",
    "Erie",
    "Lake Erie Ice Coverage",
    "Couverture des glaces du lac Érié"), args);
