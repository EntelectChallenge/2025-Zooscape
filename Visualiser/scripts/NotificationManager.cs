using System;
using System.Threading.Tasks;
using Godot;

public static class NotificationManager
{
    public static async void ShowNotification(
        Node node,
        string notificationText,
        bool isSuccess = true
    )
    {
        var state = isSuccess ? "success" : "failure";
        var toast = ResourceLoader
            .Load<PackedScene>($"res://scenes/menus/popups/toast_{state}.tscn")
            .Instantiate();
        var toastHeading = toast.GetNode<Label>("UI/Content/Panel/Heading");
        toastHeading.Text = notificationText;

        var tree = node.GetTree();
        tree.GetRoot().CallDeferred("add_child", toast);

        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        tree.GetRoot().CallDeferred("remove_child", toast);
    }
}
