package org.example;

import Models.Dtos.BotCommandDto;
import Models.Dtos.GameStateDto;
import Services.BotService;
import com.microsoft.signalr.HubConnection;
import com.microsoft.signalr.HubConnectionBuilder;
import com.microsoft.signalr.HubConnectionState;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.UUID;

public class Main {
    public static void main(String[] args) {
        Logger logger = LoggerFactory.getLogger(Main.class);
        BotService botService = new BotService();
        BotCommandDto commandDto;

        String environmentIp = System.getenv("RUNNER_IPV4");
        String environmentNickname = System.getenv("BOT_NICKNAME");
        UUID token = UUID.fromString(System.getenv("Token"));
        token = (token != null) ? token : UUID.fromString(System.getenv("REGISTRATION_TOKEN"));

        String ip = (environmentIp != null && !environmentIp.isBlank()) ? environmentIp : "localhost";
        ip = ip.startsWith("http://") ? ip : "http://" + ip;

        String url = ip + ":" + "5001" + "/bothub";

        String nickname = environmentNickname != null ? environmentNickname : "JavaBot";

        HubConnection hubConnection = HubConnectionBuilder
                .create(url)
                .build();

        hubConnection.on("Disconnect", (reason) -> {
            logger.info("Disconnected: {}", reason);
            hubConnection.stop();
        }, UUID.class);

        hubConnection.on("Registered", (id) -> {
            System.out.println("Registered with the runner, with bot ID: " + id);
            botService.setBotId(id);
        }, UUID.class);

        hubConnection.on("EndGame", (state) -> {
            System.out.println("Game complete");
        }, String.class);

        hubConnection.on("GameState", (gameStateDto) -> {
            System.out.println("Game state received");
            botService.setGameStateDto(gameStateDto);
        }, GameStateDto.class);

        hubConnection.start().blockingAwait();

        System.out.println("Registering with the bothub...");
        hubConnection.send("Register", token, nickname);

        while (hubConnection.getConnectionState() == HubConnectionState.CONNECTED) {
            GameStateDto gameState = botService.getGameStateDto();
            if (gameState == null) {
                continue;
            }
            commandDto = botService.processState(gameState);
            hubConnection.send("BotCommand", commandDto);
        }

        hubConnection.stop();
    }
}