# Entelect Challenge 2025 - Zooscape 🦏

Zooscape is the Entelect Challenge game for 2025.\
Participants are required to code bots to simulate escaped zoo animals in a virtual world. These animals are chased by zookeepers trying to return them to their enclosures.\
While avoiding the zookeepers, the animals aim to collect as many food pellets and power-ups (coming soon) as possible to achieve a high score.

## Table of Contents
- [Entelect Challenge 2025 - Zooscape 🦏](#entelect-challenge-2025---zooscape-)
  - [Table of Contents](#table-of-contents)
  - [General](#general)
  - [Prerequisites](#prerequisites)
  - [Getting Started](#getting-started)
      - [Step - 1 - Download](#step---1---download)
      - [Step 2 - Install Dependencies](#step-2---install-dependencies)
      - [Step 3 - Improve](#step-3---improve)
      - [Step 4 - Upload](#step-4---upload)
  - [Running the project](#running-the-project)
      - [Windows Command Prompt / PowerShell:](#windows-command-prompt--powershell)
      - [Linux / MacOS / Unix:](#linux--macos--unix)
  - [Project Structure](#project-structure)
  - [Submission Process](#submission-process)

## General
For game rules please refer to [Rules.md](./Rules.md)

For changelog, please refer to [Releases](https://github.com/EntelectChallenge/2025-Zooscape/releases)

## Prerequisites
- .NET 8 SDK
- Docker

## Getting Started

Follow these instructions to set up and run this project on your local machine.


#### Step - 1 - Download
Download the zip file and extract it to the folder you want to work.
#### Step 2 - Install Dependencies
In working folder run `dotnet restore`

#### Step 3 - Improve
Customize the provided logic or create your own to enhance one of the given starter bots, and then upload it to the player portal to see how it stacks up against the competition!

#### Step 4 - Upload
Sign up to the player portal [here](https://challenge.entelect.co.za/signin), and follow the steps to upload your bot and participate in tournaments!


## Running the project

The application can be run using docker via the following commands from the root of the project:

#### Windows Command Prompt / PowerShell:
```powershell
.\run.cmd
```

#### Linux / MacOS / Unix:
```powershell   
./run.sh
```

These scripts will run the game on port 5001 and connect 3 reference bots

## Project Structure

In this project you will find everything we use to build a starter pack that will assist you to run a bot on your local machine. which comprises of the following:

1. **Zooscape** - The engine enforces the game's rules by applying the bot commands to the game state if they are valid.
2. **ReferenceBot** - A bot that can be played against to test your own bot
3. **StarterBots** - Starter bots with limited logic that can be used as a starting point for your bot.
4. **PlayableBot** - A bot that can manually controlled by the player

This project can be used to get a better understanding of the rules and to help debug your bot.

Improvements and enhancements will be made to the game engine code over time.

The game engine is available to the community for peer review and bug fixes. If you find any bugs or have any concerns, please [e-mail us](mailto:challenge@entelect.co.za) or discuss it with us on the [forum](http://forum.entelect.co.za/). Alternatively submit a pull request on Github, and we will review it.


## Submission Process
We have automated submissions through GitHub!
For more information, sign up for the player portal [here](https://challenge.entelect.co.za/portal), and follow the steps!

