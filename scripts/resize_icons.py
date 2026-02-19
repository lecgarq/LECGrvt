import os
from PIL import Image

script_dir = os.path.dirname(os.path.abspath(__file__))
repo_root = os.path.dirname(script_dir)

source_dir = os.path.join(repo_root, "design", "full-size")
dest_dir = os.path.join(repo_root, "src", "Resources", "Images")

if not os.path.exists(dest_dir):
    os.makedirs(dest_dir)

# Map filenames to desired base names
mapping = {
    "Align Master.png": "AlignMaster",
    "left.png": "AlignLeft",
    "center.png": "AlignCenter",
    "right.png": "AlignRight",
    "top.png": "AlignTop",
    "middle.png": "AlignMiddle",
    "bottom.png": "AlignBottom",
    "horizontally.png": "DistributeH",
    "vertically.png": "DistributeV"
}

for filename, basename in mapping.items():
    source_path = os.path.join(source_dir, filename)
    if os.path.exists(source_path):
        try:
            with Image.open(source_path) as img:
                # Save 32x32
                img32 = img.resize((32, 32), Image.Resampling.LANCZOS)
                img32.save(os.path.join(dest_dir, f"{basename}_32.png"))
                print(f"Saved {basename}_32.png")
                
                # Save 16x16
                img16 = img.resize((16, 16), Image.Resampling.LANCZOS)
                img16.save(os.path.join(dest_dir, f"{basename}_16.png"))
                print(f"Saved {basename}_16.png")
        except Exception as e:
            print(f"Error processing {filename}: {e}")
    else:
        print(f"Source file not found: {source_path}")
