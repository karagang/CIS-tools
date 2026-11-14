import matplotlib.pyplot as plt
import cartopy.crs as ccrs
from shapely.geometry import Polygon
import cartopy.feature as cfeature

# Polygon coordinates
coords = [
    (-157.6787627634273, 70.76633838262674),
    (-163.3832906490816, 71.83439151463182),
    (-156.3338385800327, 75.21637594527468),
    (-147.195584133193, 74.06837610307107),
    (-99.6117423309124, 80.62788254998671),
    (-97.60031717135796, 78.55343488112081),
    (-94.73801176259722, 73.65216346955359),
    (-94.84879268426029, 70.49347329779454),
    (-92.41444551752413, 68.65222753607699),
    (-94.23251109695484, 66.33480395329022),
    (-96.82800112147167, 66.15753384994781),
    (-106.89340572001599, 66.11574494160803),
    (-112.70375138132987, 66.64781206702952),
    (-118.65710528217298, 67.82360122431342),
    (-128.86883183417183, 68.23065204038946),
    (-133.4805717713349, 67.01966582638971),
    (-138.52523954752718, 68.03294483413381),
    (-146.3313101089003, 69.16557440781708),
    (-157.1138729501459, 70.6316009651334),
    (-157.6787627634273, 70.76633838262674)     # CLOSE the polygon by copying first point at the end!!!
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

ax.set_title('Western Arctic')
plt.tight_layout()
plt.savefig('Western_Arctic_highlighted.png', dpi=300)
plt.show()
