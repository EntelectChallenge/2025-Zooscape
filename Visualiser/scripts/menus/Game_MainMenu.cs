using Godot;

public partial class Game_MainMenu : Button
{
    public override void _Pressed()
    {
        var scene = ResourceLoader
            .Load<PackedScene>("res://scenes/menus/main.tscn")
            .Instantiate();
        NavigationManager.NavigateToScene(scene, this);
    }
}
