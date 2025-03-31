import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import process from "process";

const runnerIP = process.env.RUNNER_IPV4 ?? "localhost";
const runnerURL = runnerIP.startsWith("http://")
    ? `${runnerIP}:5001/bothub`
    : `http://${runnerIP}:5001/bothub`;

const botNickname = process.env.BOT_NICKNAME ?? "JSBot";
const token = process.env.Token ?? process.env.REGISTRATION_TOKEN;

const state = {
    connected: false,
    botId: "",
    gameState: null
};

const connection = new HubConnectionBuilder()
    .withUrl(runnerURL)
    .configureLogging(LogLevel.Debug)
    .withAutomaticReconnect()
    .build();

connection.on("Disconnect", (reason) => {
   console.log(`Disconnected with reason: ${reason}`); 
});

connection.on("Registered", (botId) => {
    console.log(`Registered bot with ID: ${botId}`);
    state.botId = botId;
});

connection.onclose((error) => {
    console.log(`Connection closed with error: ${error}`);
});

connection.on("GameState", (state) => {
    console.log("Game state received");
});

(async () => {
    try {
        await connection.start();
        await connection.invoke("Register", token, botNickname);
    } catch (ex) {
        console.error(`Error connecting: ${ex}`);
    }
})();