from PIL import Image
import os

def extract_icons(input_path, output_dir="icons"):
    """Extract individual feather icons from the composite image"""

    # Create output directory if it doesn't exist
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)

    # Load the image
    img = Image.open(input_path)
    width, height = img.size

    print(f"Image size: {width}x{height}")

    # Define approximate regions for each icon (left, top, right, bottom)
    # These regions are estimated based on visual inspection
    icons = {
        "cyan_circuit_large": (30, 30, 450, 530),      # Top-left cyan circuit feather
        "purple_ai": (460, 30, 820, 530),               # Top-middle purple AI feather
        "metallic_dark": (840, 30, 1180, 530),          # Top-middle-right dark metallic
        "metallic_silver": (1200, 30, 1540, 530),       # Top-right silver metallic
        "cyan_circuit_small": (30, 540, 350, 800),      # Bottom-left small cyan
        "black": (400, 540, 700, 800),                  # Bottom-middle black
        "white": (750, 540, 1050, 800),                 # Bottom-right white
    }

    # Extract and save each icon
    for name, bbox in icons.items():
        try:
            # Crop the icon
            icon = img.crop(bbox)

            # Save as PNG
            output_path = os.path.join(output_dir, f"{name}.png")
            icon.save(output_path, "PNG")
            print(f"Saved: {output_path} (size: {icon.size})")

        except Exception as e:
            print(f"Error extracting {name}: {e}")

    print(f"\nExtracted {len(icons)} icons to '{output_dir}' directory")

if __name__ == "__main__":
    extract_icons("quil_multi_image.png")
