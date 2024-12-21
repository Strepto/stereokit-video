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

        // Create assets used by the app

        using VideoKitPlayer videoPlayer = new();
        videoPlayer.PlayVideoAsync(new Uri("https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/TearsOfSteel.mp4"));
        Sprite videoSprite = Sprite.FromTex( videoPlayer.InitializeSoundAndTexture(), SpriteType.Single );

        Renderer.SkyTex = Tex.FromCubemap("sky/space.ktx2");

        Pose uiPose = UI.PopupPose();
        float timestampDisplay = 0;
        // Core application loop
        SK.Run(() =>
        {
            videoPlayer.Step();
            videoPlayer.SoundPosition = uiPose.position;

            UI.WindowBegin("VideoKitPlayer", ref uiPose, new Vec2(0.4f,0));

            UI.Image(videoSprite, new Vec2(UI.LayoutRemaining.x,0));

            if (UI.Button("Play"))      videoPlayer.Play();
            UI.SameLine();
            if (UI.Button("Pause"))     videoPlayer.Pause();
            UI.SameLine();
            if (UI.Button("Skip -15s")) videoPlayer.Time -= 15_000;
            UI.SameLine();
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
