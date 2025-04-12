FROM python:3.11-slim

# Change working directory to execute the following commands
WORKDIR /app

# Copy code into this image's current working directory
COPY ./ .

# Install dependencies
RUN pip install -r requirements.txt

# The entrypoint to start the bot
ENTRYPOINT ["python", "src/main.py"]