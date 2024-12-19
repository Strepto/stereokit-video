# VideoKit

## Overview

VideoKit is an implementation of a VLC video player using the LibVLCSharp library and StereoKit for rendering. It
provides a sample interface for playing video files and handling audio playback for use in StereoKit.

The VideoKit app here is compatible with Oculus Quest and Windows.

Any online video can be played by providing the URL to the video file, or local files using local paths.

- Local paths are not supported on Android. Streaming from the web works fine for most use-cases.

## Demo

<video src="https://github.com/user-attachments/assets/821a1266-af5c-4d54-b7b5-669442339fe0" width="450"/>

## Features

`VideoKitPlayer` is a C# class designed to play video files using the LibVLCSharp library and StereoKit for rendering.
It supports audio playback and provides a simple UI for controlling video playback. The class is very hacky and a proof
of viability, not a production-ready setup.

Any improvements to the setup would be appreciated as pull requests.

- Play, pause, and seek video.
- Display video using StereoKit.
- Handle audio playback with callbacks.
- Manage video and audio resources efficiently.

## Usage

1. **ON ANDROID**: Initialize the libvlc library before initializing StereoKit. This is required to load the native
   libvlc library on Android. Add the following line to the `MainActivity.cs` file in the `Run` method:

    ```csharp
    // In Platforms/Android/MainActivity.cs/Run() method
    // Add the following line to initialize the libvlc library on android before the StereoKit initialization
    Core.Initialize(); // Load the native libvlc library
    ```

2. Create an instance of `VideoKitPlayer` and initialize it:

    ```csharp
    var player = new VideoKitPlayer();
    var texture = player.InitializeSoundAndTexture();
    // use the texture in a material (check the Program.cs for an example)
    ```

3. Play a video:

    ```csharp
    // NOTE: As of writing the video path is hardcoded in the VideoKitPlayer class...
    player.PlayVideoAsync("path/to/video.mp4");
    ```

4. Call the `Step` method in your Step loop to handle UI and rendering:

    ```csharp
    player.Step();
    ```

5. Dispose of the player when done:

    ```csharp
    player.Dispose();
    ```

## Configuration

### Video Configuration

To configure video settings such as resolution, try to tweak the constants in the `VideoKitPlayer` class.

For more details on configuring video formats, refer to
the [LibVLCSharp documentation](https://code.videolan.org/videolan/LibVLCSharp).

### Audio Configuration

Audio settings can be configured similarly by modifying the constants in the `VideoKitPlayer` class. For more details on
audio configuration, refer to the [LibVLCSharp audio documentation](https://code.videolan.org/videolan/LibVLCSharp).

## License

This project is licensed under the MIT License. See the `LICENSE` file for more details.

## Other

The project was scaffolded using the StereoKit Template. You can start your own StereoKit project template using:

```shell
mkdir "MyProjectKit"
cd "MyProjectKit"
dotnet new install StereoKit.Templates
dotnet new sk-multi
