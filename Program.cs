using StereoKit;

namespace VideoKit;

class Program
{
    static void Main(string[] args)
    {
        // Initialize StereoKit
        SKSettings settings = new SKSettings { appName = "VideoKit", assetsFolder = "Assets" };
        if (!SK.Initialize(settings))
            return;

        using var videoPlayer = new VideoKitPlayer();
        var texture = videoPlayer.InitializeSoundAndTexture();

        videoPlayer.PlayVideoAsync("");

        // Create assets used by the app
        Pose cubePose = new Pose(0, 0, -1.5f);
        Model cube = Model.FromMesh(Mesh.GenerateRoundedCube(new Vec3(16, 9, 1) / 10, 0.02f), Material.UI);

        Material videoMaterial = Material.Unlit;
        videoMaterial[MatParamName.DiffuseTex] = texture;

        // Core application loop
        SK.Run(() =>
        {
            videoPlayer.Step();
            UI.Handle("Cube", ref cubePose, cube.Bounds);

            // videoPlayer.ActiveSoundStream.Play(cubePose.position);
            cube.Draw(videoMaterial, cubePose.ToMatrix(), Color.White);
        });
    }
}
