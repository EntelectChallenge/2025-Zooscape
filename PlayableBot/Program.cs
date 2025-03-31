using Microsoft.AspNetCore.SignalR.Client;
using PlayableBot.Models;

namespace PlayableBot;

public class Program
{
    static readonly Random random = new Random();

    private static async Task Main(string[] args)
    {
        var ui = new UI();

        var connection = new HubConnectionBuilder().WithUrl("http://localhost:5001/bothub").Build();

        connection.Closed += async (error) =>
        {
            Console.WriteLine("Connection closed.");
            await Task.Delay(5000);
            await connection.StartAsync();
        };

        connection.On<Guid>(
            "Registered",
            (token) =>
            {
                Console.WriteLine($"{token}: registered");
            }
        );

        connection.On<GameState>(
            "GameState",
            (message) =>
            {
                ui.DrawGrid(message.Cells, message.Animals, message.Zookeepers);
            }
        );

        try
        {
            // Start the connection to the SignalR hub
            await connection.StartAsync();
            Console.WriteLine("Connected to SignalR hub!");

            // Send a "Register" message to the server
            await connection.InvokeAsync("Register", Guid.NewGuid(), GenerateRandomName());
            Console.WriteLine("Sent Register message.");

            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                BotAction action = BotAction.Up;

                switch (keyInfo.Key)
                {
                    case ConsoleKey.W:
                        action = BotAction.Up;
                        break;
                    case ConsoleKey.A:
                        action = BotAction.Left;
                        break;
                    case ConsoleKey.S:
                        action = BotAction.Down;
                        break;
                    case ConsoleKey.D:
                        action = BotAction.Right;
                        break;
                }

                if (keyInfo.Key == ConsoleKey.Escape)
                    break;

                // Send the key press to the SignalR hub
                await connection.SendAsync("BotCommand", new BotCommand() { Action = action });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to SignalR hub: {ex.Message}");
        }
        finally
        {
            // Close the connection when done
            await connection.StopAsync();
            await connection.DisposeAsync();
            Console.WriteLine("Connection closed.");
        }
    }

    private static string GenerateRandomName()
    {
        // Define two lists of words
        string[] firstList =
        {
            "Big",
            "Lil'",
            "Blue",
            "Stupid",
            "Stinky",
            "Fuzzy",
            "Squishy",
            "Phat",
        };
        string[] secondList = { "Ol'", "Red", "Rotten", "Wooden", "Russian", "Party" };
        string[] thirdList = { "Dog", "Bugger", "Robot", "Muppet", "Blob", "Crawler", "Snotnose" };

        // Pick a random word from each list
        string firstWord = firstList[random.Next(firstList.Length)];
        string secondWord = secondList[random.Next(secondList.Length)];
        string thirdWord = thirdList[random.Next(thirdList.Length)];

        // Combine the words to form a name
        return $"{firstWord} {secondWord} {thirdWord}";
    }

    public class BotCommand
    {
        public BotAction Action { get; set; }
    }

    public enum BotAction
    {
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
    }
}
