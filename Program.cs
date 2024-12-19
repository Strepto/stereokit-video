using StereoKit;
using System;

namespace VideoKit;

class Program
{
    static void Main(string[] args)
    {
        // Initialize StereoKit
        SKSettings settings = new SKSettings { appName = "VideoKit", assetsFolder = "Assets" };
        if (!SK.Initialize(settings))
            return;

        using VideoKitPlayer videoPlayer = new();
        videoPlayer.PlayVideoAsync(new Uri("https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/TearsOfSteel.mp4"));

        Material videoMaterial = Material.Unlit.Copy();
        videoMaterial[MatParamName.DiffuseTex] = videoPlayer.InitializeSoundAndTexture();

        // Create assets used by the app
        Pose cubePose = new Pose(0, 0, -1.5f);
        Model cube = Model.FromMesh(Mesh.GenerateRoundedCube(new Vec3(16, 9, 1) / 10, 0.02f), videoMaterial);

        Renderer.SkyTex = Tex.FromCubemap("sky/space.ktx2");

        Pose uiPose = UI.PopupPose();
        float timestampDisplay = 0;
        // Core application loop
        SK.Run(() =>
        {
            videoPlayer.Step();

            UI.Handle("Cube", ref cubePose, cube.Bounds);
            videoPlayer.SoundPosition = cubePose.position;
            cube.Draw(cubePose.ToMatrix());

            UI.WindowBegin("VideoKitPlayer", ref uiPose, Vec2.UnitX * 0.3f);

            if (UI.Button("Play"))      videoPlayer.Play();
            if (UI.Button("Pause"))     videoPlayer.Pause();
            if (UI.Button("Skip -15s")) videoPlayer.Time -= 15_000;
            if (UI.Button("Skip +15s")) videoPlayer.Time += 15_000;

            if (UI.HSlider("timer", ref timestampDisplay, 0, videoPlayer.Length, step: 0, notifyOn: UINotify.Finalize))
                videoPlayer.Time = (long)timestampDisplay;
            // Don't update the seek time if the user is interacting with it.
            if (!UI.LastElementActive.IsActive())
                timestampDisplay = videoPlayer.Time;

            TimeSpan elapsed = TimeSpan.FromMilliseconds(videoPlayer.Time);
            TimeSpan total   = TimeSpan.FromMilliseconds(videoPlayer.Length);
            UI.Text( $"Time: {elapsed.ToString(@"hh\:mm\:ss")}/{total.ToString(@"hh\:mm\:ss")}" );

            UI.WindowEnd();
        });
    }
}
