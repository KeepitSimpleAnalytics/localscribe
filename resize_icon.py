from PIL import Image
import os

def create_icon_sizes(input_path="single_ls.png", output_dir="ls_taskbar_icons"):
    """Resize the image to standard taskbar icon sizes"""

    # Create output directory if it doesn't exist
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)

    # Load the image
    img = Image.open(input_path)
    print(f"Original image size: {img.size}")

    # Standard taskbar icon sizes
    icon_sizes = [256, 128, 64, 48, 32, 16]

    # Create resized versions
    for size in icon_sizes:
        # Resize the image maintaining aspect ratio
        img_resized = img.copy()
        img_resized.thumbnail((size, size), Image.Resampling.LANCZOS)

        # Save the resized image
        output_filename = f"ls_icon_{size}x{size}.png"
        output_path = os.path.join(output_dir, output_filename)
        img_resized.save(output_path, "PNG")
        print(f"Created: {output_filename} ({img_resized.size[0]}x{img_resized.size[1]})")

    print(f"\nAll icons saved to '{output_dir}' directory")

if __name__ == "__main__":
    create_icon_sizes()
