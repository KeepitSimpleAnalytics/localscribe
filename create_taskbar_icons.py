from PIL import Image
import os

def create_taskbar_icons(input_dir="icons", output_dir="taskbar_icons"):
    """Create taskbar-sized icons from extracted images"""

    # Create output directory if it doesn't exist
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)

    # Standard taskbar icon sizes
    icon_sizes = [256, 128, 64, 48, 32, 16]

    # Process each extracted icon
    for filename in os.listdir(input_dir):
        if filename.endswith('.png'):
            input_path = os.path.join(input_dir, filename)
            base_name = filename.replace('.png', '')

            try:
                img = Image.open(input_path)

                # Create a version for each standard size
                for size in icon_sizes:
                    # Create a square canvas with transparency
                    square_img = Image.new('RGBA', (size, size), (0, 0, 0, 0))

                    # Resize the original image to fit within the square while maintaining aspect ratio
                    img_copy = img.copy()
                    img_copy.thumbnail((size, size), Image.Resampling.LANCZOS)

                    # Calculate position to center the image
                    x = (size - img_copy.width) // 2
                    y = (size - img_copy.height) // 2

                    # Paste the resized image onto the square canvas
                    square_img.paste(img_copy, (x, y), img_copy if img_copy.mode == 'RGBA' else None)

                    # Save with size in filename
                    output_filename = f"{base_name}_{size}x{size}.png"
                    output_path = os.path.join(output_dir, output_filename)
                    square_img.save(output_path, "PNG")
                    print(f"Created: {output_filename}")

            except Exception as e:
                print(f"Error processing {filename}: {e}")

    print(f"\nTaskbar icons saved to '{output_dir}' directory")

if __name__ == "__main__":
    create_taskbar_icons()
