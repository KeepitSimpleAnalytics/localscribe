from PIL import Image, ImageChops
import os

def extract_feather_icon(input_path="single_ls.png", output_dir="final_icon"):
    """Extract the LS circuit feather icon and create taskbar sizes"""

    # Create output directory if it doesn't exist
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)

    # Load the image
    img = Image.open(input_path)
    width, height = img.size
    print(f"Original image size: {width}x{height}")

    # Convert to RGBA for transparency handling
    if img.mode != 'RGBA':
        img = img.convert('RGBA')

    # Get the pixel data
    pixels = img.load()

    # The background appears to be dark blue (around RGB: 10-20, 20-40, 50-80)
    # Let's find the bounds of the bright cyan feather

    # Find the bounding box by looking for bright cyan pixels
    min_x, min_y = width, height
    max_x, max_y = 0, 0

    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            # Look for bright cyan/blue pixels (the circuit pattern)
            # Bright pixels have high B and G values
            brightness = (r + g + b) / 3
            if brightness > 50:  # Bright enough to be part of the feather
                min_x = min(min_x, x)
                min_y = min(min_y, y)
                max_x = max(max_x, x)
                max_y = max(max_y, y)

    # Add some padding
    padding = 40
    min_x = max(0, min_x - padding)
    min_y = max(0, min_y - padding)
    max_x = min(width, max_x + padding)
    max_y = min(height, max_y + padding)

    print(f"Feather bounds: ({min_x}, {min_y}) to ({max_x}, {max_y})")

    # Crop to the feather
    feather = img.crop((min_x, min_y, max_x, max_y))
    print(f"Cropped feather size: {feather.size}")

    # Save the extracted feather with dark background
    feather_with_bg = os.path.join(output_dir, "ls_feather_with_bg.png")
    feather.save(feather_with_bg, "PNG")
    print(f"Saved: {feather_with_bg}")

    # Create a version with transparent background
    # Make dark blues transparent
    feather_transparent = feather.copy()
    pixels_trans = feather_transparent.load()

    for y in range(feather_transparent.height):
        for x in range(feather_transparent.width):
            r, g, b, a = pixels_trans[x, y]
            brightness = (r + g + b) / 3
            # Make dark pixels transparent
            if brightness < 50:
                pixels_trans[x, y] = (r, g, b, 0)

    feather_transparent_path = os.path.join(output_dir, "ls_feather_transparent.png")
    feather_transparent.save(feather_transparent_path, "PNG")
    print(f"Saved: {feather_transparent_path}")

    return feather, feather_transparent

def create_icon_sizes(source_img, output_dir="final_icon", prefix="ls_icon"):
    """Create standard icon sizes"""

    icon_sizes = [256, 128, 64, 48, 32, 16]

    print(f"\nCreating {prefix} in multiple sizes...")
    for size in icon_sizes:
        # Create a square canvas with transparency
        square_img = Image.new('RGBA', (size, size), (0, 0, 0, 0))

        # Resize the original image to fit within the square while maintaining aspect ratio
        img_copy = source_img.copy()
        img_copy.thumbnail((size, size), Image.Resampling.LANCZOS)

        # Calculate position to center the image
        x = (size - img_copy.width) // 2
        y = (size - img_copy.height) // 2

        # Paste the resized image onto the square canvas
        square_img.paste(img_copy, (x, y), img_copy)

        # Save with size in filename
        output_filename = f"{prefix}_{size}x{size}.png"
        output_path = os.path.join(output_dir, output_filename)
        square_img.save(output_path, "PNG")
        print(f"Created: {output_filename}")

if __name__ == "__main__":
    print("Extracting LS feather icon...\n")
    feather_bg, feather_trans = extract_feather_icon()

    print("\n" + "="*50)
    print("Creating icons with dark background...")
    print("="*50)
    create_icon_sizes(feather_bg, prefix="ls_icon_dark")

    print("\n" + "="*50)
    print("Creating icons with transparent background...")
    print("="*50)
    create_icon_sizes(feather_trans, prefix="ls_icon_transparent")

    print("\n" + "="*50)
    print("Done! All icons saved to 'final_icon' directory")
    print("="*50)
