from PIL import Image

def main():
    # Load input JPG image
    input_img = Image.open('WaterchumpLoop-13x13.jpg').convert('RGB')
    
    # Split into RGB channels
    r, g, b = input_img.split()
    
    # Create lookup table for alpha channel
    lookup_table = []
    for i in range(256):
        if i <= 51:  # 0.2 * 255 = 51
            # Map [0,51] to [0,255] linearly: alpha = i * 5
            lookup_table.append(min(i * 5, 255))
        else:
            lookup_table.append(255)
    
    # Apply lookup table to blue channel to create alpha
    alpha = b.point(lookup_table)
    
    # Merge channels into RGBA image
    output_img = Image.merge('RGBA', (r, g, b, alpha))
    
    # Save as PNG with alpha channel
    output_img.save('WaterchumpLoop-13x13.png')

if __name__ == '__main__':
    main()