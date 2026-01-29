from PIL import Image
import os

def extract_ls_feather(input_path="ls_quil.png", output_dir="ls_icon"):
    """Extract the large LS embossed feather icon"""

    # Create output directory if it doesn't exist
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)

    # Load the image
    img = Image.open(input_path)
    width, height = img.size

    print(f"Image size: {width}x{height}")

    # The large LS embossed feather is on the left side
    # Based on visual inspection, it appears to occupy the left panel
    # Let's crop with some padding to ensure we get the complete feather
    # The left section appears to be roughly from 0 to ~744 (about 1/3 of 2234 width)

    # Extract the feather with generous bounds to avoid cropping
    feather_bbox = (0, 0, 744, height)  # Left section of the image

    feather = img.crop(feather_bbox)

    # Find the actual bounds of the feather by looking for non-background pixels
    # Convert to RGBA if not already
    if feather.mode != 'RGBA':
        feather = feather.convert('RGBA')

    # Get the bounding box of non-transparent/non-background content
    # First, let's save the initial crop
    initial_path = os.path.join(output_dir, "ls_feather_initial.png")
    feather.save(initial_path, "PNG")
    print(f"Saved initial crop: {initial_path} (size: {feather.size})")

    # Now let's try to auto-crop to remove excess background
    # Get pixel data
    bbox = feather.getbbox()
    if bbox:
        feather_cropped = feather.crop(bbox)
        final_path = os.path.join(output_dir, "ls_feather.png")
        feather_cropped.save(final_path, "PNG")
        print(f"Saved cropped feather: {final_path} (size: {feather_cropped.size})")
        return feather_cropped
    else:
        print("Could not auto-crop, using initial crop")
        return feather

def create_icon_sizes(source_img, output_dir="ls_icon"):
    """Create standard icon sizes from the source feather image"""

    icon_sizes = [256, 128, 64, 48, 32, 16]

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
        output_filename = f"ls_feather_{size}x{size}.png"
        output_path = os.path.join(output_dir, output_filename)
        square_img.save(output_path, "PNG")
        print(f"Created: {output_filename}")

if __name__ == "__main__":
    feather = extract_ls_feather()
    print("\nCreating taskbar-sized versions...")
    create_icon_sizes(feather)
    print("\nDone! Icons saved to 'ls_icon' directory")
