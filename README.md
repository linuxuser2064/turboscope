# Turboscope
An oscilloscope view program made in VB.net (and a bit of C#).

## Features:
- Video export
- Peak speed triggering
- Native CRT scope effect without resolution tricks
- Customizable channel positioning and sizing (x/y/width/height are all customizable)
- Custom line color and background color
- Single-frame preview
- Background image support

## Building:
Just open the solution in VS 2022 and build.
To make video export work, you'll need FFmpeg 6.x binaries (non-static): https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n6.1-latest-win64-gpl-shared-6.1.zip.
They need to be put under `ffmpeg\x86_64` in the folder of the program.

## Credits:
- radek-k for FFMediaToolkit (https://github.com/radek-k/FFMediaToolkit)
- Ruslan-B for FFmpeg.AutoGen (https://github.com/Ruslan-B/FFmpeg.AutoGen)
- The FFmpeg team for FFmpeg (https://www.ffmpeg.org/)
