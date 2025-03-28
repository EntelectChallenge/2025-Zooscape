# Zooscape JavaScript Starter Bot

## Installation

To get started, install dependencies by running the following command

```sh
pip install -r requirements.txt
```

## Developing

Edit the code in `src/main.py` to make your bot go! 

## Running the bot

To run the bot simply run

```sh
python src/main.py
```

### Docker
Build the docker image by running `docker build -t <image_name> .` in the root directory i.e. /PythonBot  
Then run the container using `docker run --env=RUNNER_IPV4=host.docker.internal pybot`. Be sure to have the engine running before you run your bot.  
You can change the container name by adding the [`--name`](https://docs.docker.com/engine/reference/commandline/run/#name) option to the run command.  
You can also change the name of the JSBot in the logs by adding the `--env=BOT_NICKNAME=MyBotName` option to the run command  