using Godot;

public partial class Main_LinkToLogs : Button
{
    public override void _Pressed()
    {
        var fileSelectorScene = ResourceLoader
            .Load<PackedScene>("res://scenes/menus/popups/file_selector.tscn")
            .Instantiate();

        var fileDialog = fileSelectorScene.GetNode<FileDialog>("FileDialog");

        fileDialog.Show();
        if (fileDialog?.CurrentFile?.Length == 0)
        {
            return;
        }

        GD.Print($"File Directory: {fileDialog.CurrentFile}");

        if (!fileDialog.CurrentFile.EndsWith(".log"))
        {
            NotificationManager.ShowNotification(this, "Please select a valid log file!", false);
            return;
        }

        // FileLoader.Instance.LoadObject(fileDialog.CurrentFile);

        GameSettings.LogsLocation = fileDialog.CurrentFile;
        GameSettings.ReadFromLogs = true;
        GameSettings.GameSpeed = 5.0f;

        var worldScene = ResourceLoader.Load<PackedScene>("res://scenes/world.tscn").Instantiate();
        NavigationManager.NavigateToScene(worldScene, this);
        NotificationManager.ShowNotification(worldScene, "Logs loaded successfully!");
    }
}
