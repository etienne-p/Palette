


# Palette
Using indexed colors in Unity.

When working with cartoonish assets, sometimes mobile compression gives poor looking results and indexed colors may be a good option.
## Usage

Right click the texture you'd like to encode (in your assets panel), select "Generate Indexed Color Sprite".

A window will pop-up. You'll be able to control the color palette generation:

**Tolerance Factor**: the tolerance factor to decide on whether two source image colors can be considered the same or should be represented by two separate palette colors

**Alpha Threshold**: we use one bit for alpha, every pixel whose alpha value is below this threshold will be completely transparent, others will be completely opaque.

Then, just click "Generate Indexed", an alpha8 texture and a material will be generated, update them both in your SpriteRenderer component.

