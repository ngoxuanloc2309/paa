from PIL import Image, ImageDraw
import os

repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
app_icon_path = os.path.join(repo_root, 'SimplePLC.Studio', 'Assets', 'app.png')
output_dir = os.path.join(repo_root, 'installer')

logo = Image.open(app_icon_path).convert('RGBA')

# 1. wizard_small.bmp (55x55): Upper-right header icon on white background
small_canvas = Image.new('RGB', (55, 55), (255, 255, 255))
logo_small = logo.resize((42, 42), Image.Resampling.LANCZOS)
small_canvas.paste(logo_small, ((55 - 42) // 2, (55 - 42) // 2), logo_small)
small_canvas.save(os.path.join(output_dir, 'wizard_small.bmp'), 'BMP')
print("Successfully generated installer/wizard_small.bmp (55x55)")

# 2. wizard_large.bmp (164x314): Left banner for Welcome & Finished pages
# Industrial Slate #F8FAFC with crisp logo and subtle 1px divider on the right
large_canvas = Image.new('RGB', (164, 314), (248, 250, 252)) # #F8FAFC
draw = ImageDraw.Draw(large_canvas)

# Resize logo to 80x80
logo_large = logo.resize((80, 80), Image.Resampling.LANCZOS)
large_canvas.paste(logo_large, ((164 - 80) // 2, 60), logo_large)

# Subtle 1px right border line #E2E8F0
draw.line([(163, 0), (163, 313)], fill=(226, 232, 240), width=1)

large_canvas.save(os.path.join(output_dir, 'wizard_large.bmp'), 'BMP')
print("Successfully generated installer/wizard_large.bmp (164x314)")
