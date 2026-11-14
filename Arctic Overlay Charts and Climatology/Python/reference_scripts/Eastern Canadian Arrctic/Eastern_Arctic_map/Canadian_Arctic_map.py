import matplotlib.pyplot as plt
import cartopy.crs as ccrs
from shapely.geometry import Polygon
import cartopy.feature as cfeature

# Polygon coordinates
coords = [
    (-87.78551534385531, 66.67041848580936),
    (-92.27026027641529, 67.85513948225008),
    (-94.91974860117139, 71.11225653263347),
    (-94.7488087005857, 72.43020023616691),
    (-98.49880257157213, 74.07949377109344),
    (-98.14396398958328, 75.25637630414747),
    (-99.5189164590465, 76.27655930967362),
    (-100.22022747416992, 78.21867945385401),
    (-102.80636855691077, 78.89263282724804),
    (-105.75289775286492, 80.06567751744961),
    (-94.38265001608619, 81.12497525230339),
    (-86.40688501681117, 81.73901493879325),
    (-79.4560454914012, 81.71531632735937),
    (-65.13776411553897, 82.4362176402),
    (-57.80213585658555, 81.84587263284516),
    (-60.91541464791959, 79.67119810649967),
    (-64.50396646655975, 77.90759200507947),
    (-62.49057003696376, 76.70191086744478),
    (-57.0778898026618, 76.1273640430492),
    (-52.40247844563805, 72.5680676353031),
    (-52.66322279122308, 70.3939345880463),
    (-53.52540957943344, 69.55440647155922),
    (-55.27681799517012, 64.560754098279),
    (-66.15367969399153, 64.6051940617426),
    (-70.82903198754637, 65.45651456114823),
    (-72.53565947156572, 65.71334351115593),
    (-73.58263425556811, 64.90431186295629),
    (-77.46454180057839, 64.53135498643027),
    (-83.01119287692876, 64.48489245308932),
    (-88.60683553236845, 64.651944991352),
    (-90.4630421388074, 65.91569405317634),
    (-87.96282871125442, 66.60635977822585),
    (-87.78551534385531, 66.67041848580936)     # CLOSE the polygon by copying first point at the end!!!
]

polygon = Polygon(coords)

# Set up the figure and axes
fig = plt.figure(figsize=(12, 10))
ax = plt.axes(projection=ccrs.LambertConformal(
    central_longitude=-100, central_latitude=65,
    standard_parallels=(55, 75)))
ax.set_extent([-170, -50, 51, 83], crs=ccrs.PlateCarree())
ax.add_feature(cfeature.BORDERS, linewidth=1, edgecolor='black')
ax.add_feature(cfeature.COASTLINE, linewidth=0.5)
ax.add_feature(cfeature.LAND, facecolor='lightgray')

# Add the polygon
ax.add_geometries([polygon], ccrs.PlateCarree(),
                  facecolor='red', edgecolor='darkred', alpha=0.5)

ax.set_title('Eastern Arctic')
plt.tight_layout()
plt.savefig('Eastern_Arctic_highlighted.png', dpi=300)
plt.show()
