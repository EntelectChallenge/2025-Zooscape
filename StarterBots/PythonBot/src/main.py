import os
import logging
import json
from signalrcore.hub_connection_builder import HubConnectionBuilder
from BotAction import BotAction
import time
import uuid

# Environment variables
runner_ip = os.environ.get('RUNNER_IPV4', 'localhost')

# Convert HTTP URL to WebSocket URL (ws:// or wss://)
if runner_ip.startswith('http://'):
    websocket_url = runner_ip.replace('http://', 'ws://') + ':5000/bothub'
elif runner_ip.startswith('https://'):
    websocket_url = runner_ip.replace('https://', 'wss://') + ':5000/bothub'
else:
    websocket_url = f"ws://{runner_ip}:5000/bothub"

bot_nickname = os.environ.get('BOT_NICKNAME', 'PythonBot')
token = os.environ.get('Token') or os.environ.get('REGISTRATION_TOKEN') or str(uuid.uuid4())

state = {
    'connected': False,
    'bot_id': '',
    'game_state': None
}

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Create SignalR connection
hub_connection = HubConnectionBuilder()\
    .with_url(websocket_url)\
    .configure_logging(logging.INFO)\
    .build()

# Event handlers
def on_open():
    logger.info('Connection opened and handshake received ready')

    # Register the bot
    logger.info(f"Registering bot with nickname: {bot_nickname} and token: {token[:5]}...")

    hub_connection.send('Register', [token, bot_nickname])
    logger.info('Registration command sent')

def on_disconnect(reason):
    logger.info(f"Disconnected with reason: {reason}")

def on_registered(bot_id):
    logger.info(f"Registered bot with ID: {bot_id[0]}")
    state['bot_id'] = bot_id[0]

def on_game_state(game_state):
    # Log what we received to help debug
    logger.debug(f"Game state received - Type: {type(game_state)}")

    # Store the tick count for sequencing
    current_tick = state.get('tick_count', 0) + 1
    state['tick_count'] = current_tick

    # Handle different possible formats
    if isinstance(game_state, str):
        try:
            game_state = json.loads(game_state)
        except json.JSONDecodeError:
            logger.error('Failed to parse game state JSON string')
            return

    # If game_state is a list, extract the first item if available
    if isinstance(game_state, list):
        logger.debug(f"Game state is a list with {len(game_state)} items")
        if len(game_state) > 0:
            game_state = game_state[0]
            logger.debug('Using first item from game state list')
        else:
            logger.error('Empty game state list received')
            return

    # Now we should have a dictionary
    if not isinstance(game_state, dict):
        logger.error(f"Unexpected game state type: {type(game_state)}")
        return

    # Log and store the game state
    logger.debug(f"Processing game state - Tick: {game_state.get('tick', 'unknown')}")
    state['game_state'] = game_state

    # Process game state and decide on next move
    try:
        # Find our animal in the game state
        our_animal = None
        for animal in game_state.get('animals', []):
            if animal.get('id') == state['bot_id']:
                our_animal = animal
                break

        if our_animal:
            logger.info(f"Score: {our_animal.get('score')}, Captured {our_animal.get('capturedCounter')} times")

            # Make a move decision
            next_move = decide_move(our_animal, game_state)

            # Create proper BotCommand object as expected by the server
            bot_command = {
                'Action': next_move
            }

            # Send the move command with proper object format
            if next_move:
                logger.debug(f"Sending move: {next_move}")
                hub_connection.send('BotCommand', [bot_command])
    except Exception as ex:
        logger.error(f"Error processing game state: {ex}")
        import traceback
        traceback.print_exc()

def decide_move(our_animal, game_state):
    """Logic to decide the next move for our animal"""

    # Send a random move
    import random
    return random.choice([BotAction.UP, BotAction.DOWN, BotAction.LEFT, BotAction.RIGHT])

# Register event handlers
hub_connection.on('Disconnect', on_disconnect)
hub_connection.on('Registered', on_registered)
hub_connection.on('GameState', on_game_state)

hub_connection.on_open(on_open)
hub_connection.on_close(lambda: print('Connection closed'))
hub_connection.on_error(lambda data: print(f"An exception was thrown closed{data.error}"))

# Start connection
if __name__ == '__main__':
    try:
        logger.info(f"Connecting to SignalR hub at: {websocket_url}")

        # Start the connection
        hub_connection.start()

        # Keep the connection open
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        hub_connection.stop()
        logger.info('Bot stopped by user')
    except Exception as ex:
        logger.error(f"Error in main loop: {ex}")
        import traceback
        traceback.print_exc()
