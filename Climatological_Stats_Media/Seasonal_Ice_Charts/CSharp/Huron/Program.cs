using GreatLakesCharts;

return ChartGenerator.Run(new LakeConfig(
    "huron_with_average.csv",
    "Huron",
    "Lake Huron Ice Coverage",
    "Couverture des glaces du lac Huron"), args);
