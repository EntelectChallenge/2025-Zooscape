using Godot;

public partial class Main_LinkToGame : Button
{
    public override void _Pressed()
    {
        GameSettings.ReadFromLogs = false;
        GameSettings.GameSpeed = 4.0f;

        var scene = ResourceLoader.Load<PackedScene>("res://scenes/world.tscn").Instantiate();
        NavigationManager.NavigateToScene(scene, this);
    }
}
