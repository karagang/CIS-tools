import matplotlib.pyplot as plt
import cartopy.crs as ccrs
from shapely.geometry import Polygon
import cartopy.feature as cfeature

# Polygon coordinates
coords = [
    (-161.50260668684928, 70.169167071506),
    (-165.0692555041342, 70.28693770731351),
    (-160.0342003650647, 73.52976026035253),
    (-149.54481043974926, 72.45588563136323),
    (-93.43370934665693, 81.58732159649733),
    (-61.46910046440398, 82.30399726216635),
    (-59.103760125461534, 81.92374257623263),
    (-65.3897998862019, 76.04172895912012),
    (-57.63617179087473, 75.70254008545243),
    (-53.11011331195441, 70.0831882474925),
    (-56.766125650384396, 58.87403599299651),
    (-63.128185964791356, 57.76827424405238),
    (-76.32756108624095, 58.51991672750361),
    (-75.8370996307488, 55.53125983819194),
    (-77.87595808284892, 52.57134858031617),
    (-79.7956844396286, 51.00728548559832),
    (-82.36523801494876, 51.359799379190065),
    (-92.60932075089094, 56.27985498775067),
    (-96.40606056003982, 58.928732977497646),
    (-94.6678279990774, 63.260317814914345),
    (-88.52855360665222, 65.60611380315015),
    (-90.10807193031893, 67.26238238594175),
    (-96.0564600125205, 66.71303711517484),
    (-110.17683844108934, 66.46625316110811),
    (-122.08013520012558, 68.20637845832971),
    (-129.8938855511999, 68.2894235634991),
    (-138.33727636176616, 67.69771258137388),
    (-161.10153624215002, 70.32109060047233),
    (-161.50260668684928, 70.169167071506)  # CLOSE the polygon
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

ax.set_title('Northern Canadian Waters    |    Eaux du Nord canadien')
plt.tight_layout()
plt.savefig('northern_canada_highlighted.png', dpi=300)
plt.show()
