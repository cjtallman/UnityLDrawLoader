# Unity LDraw Loader

An editor tool for loading LDraw files into a Unity project.

## Features

- [x] Load `.ldr`, `.mpd`, and `.dat` files from the LDraw parts library.
- [x] Asset caching for faster part loading.
- [x] Load URP compatible materials.
- [ ] Load HDRP compatible materials. (In progress)
- [ ] Load stickers/textures from LDraw files. (In progress)
- [ ] Automatic material assignment based on .ldr file color codes. (Planned)
- [ ] Support for fancy colors like transparent and rubber. (Planned)

## Usage

1. Download the LDraw parts library from [LDraw.org](https://www.ldraw.org/) and extract it to a known location on your computer.
2. Open Unity and navigate to `Tools > LDraw Part Loader` to open the window.
3. Set the path to your LDraw parts library.
4. Search for a part by its number.
5. Click the "Load Part" button to create a new asset in your project.
6. Use the loaded part in your scenes and assign materials as needed.

## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE.md) file for details.