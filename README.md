# Unity LDraw Loader

An editor tool for loading LDraw files into a Unity project.

## Features

- [x] Load `.ldr`, `.mpd`, and `.dat` files from the LDraw parts library.
- [x] Asset caching for faster part loading.
- [x] Load URP compatible materials.
- [X] Load HDRP compatible materials.
- [X] Load Built-in Render Pipeline compatible materials.
- [x] Support for fancy colors like transparent and rubber.
- [x] Automatic material assignment based on .ldr file color codes.
- [ ] Load stickers/textures from LDraw files. (In progress)

## Usage

### Setup

1. Download the LDraw parts library from [LDraw.org](https://www.ldraw.org/) and extract it to a known location on your computer.
2. Open Unity and navigate to `Tools > LDraw Part Loader` to open the LDraw Loader window.
3. Set the path to your LDraw parts library using the "Browse" button.

### Model Loader Tab

1. Click "Browse" to select an LDraw model file (`.ldr` or `.mpd`).
2. View model information including name, description, author, and parts count.
3. Explore parts used in the model organized by:
   - **By Count**: Groups parts by part number with quantity
   - **By Color**: Shows parts grouped by both part number and color
4. Click "Load Model" to create a prefab asset in `Assets/LDraw/Models/`.

![Model Loader Tab](.github/screenshots/model_loader.png)

### Part Loader Tab

1. Use the search field to find specific parts by filename or part number.
2. Navigate through paginated results (50 items per page).
3. Select a part from the list to highlight it.
4. Configure options:
   - **Show Duplicate Dialog**: Toggle confirmation when overwriting existing assets
   - **Smoothing Angle**: Adjust mesh smoothing threshold (0-180 degrees)
5. Click "Load Part" to create a mesh asset in `Assets/LDraw/Parts/`.

![Part Loader Tab](.github/screenshots/part_loader.png)

### Material Loader Tab

1. Browse all available LDraw colors loaded from `LDConfig.ldr`.
2. View color information including code, name, and HEX value.
3. Select a color from the list.
4. Click "Load Material" to create a material asset in `Assets/LDraw/Materials/`.

![Material Loader Tab](.github/screenshots/material_loader.png)

### Asset Organization

The tool automatically creates and organizes assets in the following folders:
- `Assets/LDraw/Parts/` - Individual part meshes
- `Assets/LDraw/Models/` - Complete model prefabs
- `Assets/LDraw/Materials/` - LDraw color materials

### Render Pipeline Support

Materials are automatically created compatible with your project's render pipeline:
- **Built-in Render Pipeline**: Standard shaders
- **Universal Render Pipeline (URP)**: URP-compatible materials
- **High Definition Render Pipeline (HDRP)**: HDRP-compatible materials

### Material Features

- Support for all LDraw material finishes including transparent, chrome, metallic, rubber, and more
- Automatic color assignment based on LDraw color codes
- Proper handling of transparent and special materials

## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE.md) file for details.
