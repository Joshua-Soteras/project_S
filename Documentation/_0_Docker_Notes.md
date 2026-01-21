# Docker Notes

Joshua Soteras
Created: Januaray 2026
ver. 1.0.1

--- 
## Overview 

**Purpose**
This document is meant to review Docker at a high level in order to understand the fundamentals. Furthermore, this should provide  the reader on what areas they might want o dive deeper into with exploring containers.

**What is Docker?**
Docker is a way to deploy your code/application in any machine. Essentialy setting up an "image" of what your software application needs on your machine to another machine. 

**Analogy** 
- Docker is a way to pack, ship, or containerize goods (code) to any ship (machine)
- The captain doesn't care what is in each container, as long as they can stack, move and store it. 

**Reasons to use Docker / Containerize your code**
- simplifies sharing the code 
- avoid installing dependencies and necessary things to run the code
- Cloud: Solves the problem that the application doesn't scale
- Local: Solves the problem, "It works on machine and not yours"
- Virtual Machines use too much resources / powers, Docker solves this problem running under the same OS but using the kernel to allocate resources to run the container. 

**Use Case Examples**
- **Example**: Testing Local Database
	- setting up Postgres database
	- Others who need to test code can run the same test/throw away db without needing to worry about the set up 
	
- **Example**: If you are a backend developer and need to test api endpoints with frontend
	- You do not need to worry about installing and dependencies of running the frontend. 
	- containerizing it will allow you run it without worrying about dependencies or set up .

--- 

## Sources

- Website of Docker 
	- https://www.docker.com

- Good Overview / Fundamentals of Docker
	- Video: The Only Docker Tutorial You Need. 
	- https://www.youtube.com/watch?v=DQdB7wFEygo
	
-   Introduction, Overview, Examples of use cases 
	- 100+ Docker Concepts you Need to Know - FireShip 
	- https://www.youtube.com/watch?v=rIrNIzy6U_g 
	
-  Data Persistence with Docker: Volumes vs Bind Mounts 
	- https://www.youtube.com/watch?v=LpsQgZzG0no&t=1s

--- 

## Three Main Components of Docker 

1. **Docker File** (Recipe)
	1. *The recipe on the paper*
	- The instructions on how to build the image (meal)
	
2. **Image** (Prepped Meal that is packaged and frozen )
	- *The meal cooked and packaged following the recipe* 
	- tools and instructions to run the code
	- runtimes
	- technologies we need 

3. **Container**
	- *A single Heated up and ready to eat meal* 
	- What gets made from the recipe
	- Containers are stateless
		- once the program/container closes, all the data inside them is lost

--- 

# Details of The Three Components 

Dockerfile -> Creates Image -> Runs as a Container 

1. **Docker File**
	- *see next section for creating Docker File*
	- blueprint and configure the environment

	- Layer Caching (this is the magic of the Docker File)
		- Docker executes lines one by one 
		- Example: 
			- If you change line 5 of your code, Docker *still* uses the cached results of lines 1-4. 
		
	- More about layering 
		- This is what makes up the image
		- Layers make up the Image
		- each layer is a snapshot of a single or multiple dependencies 
		- this is why ordering is important in the instructions 
		- make s
	- Ensure you Copy dependency files first before copying your source code for efficiency
		

**Examples of Layering Importance (Think of Dominion Effect)**

*Slow way* 
```
COPY . .                      # Layer 1: Copies your code (Changes often!)
RUN pip install pandas numpy  # Layer 2: Installs heavy libraries (Takes 5 mins)```
```

- If anything changes where you are copying the code then we would have to rebuild layer1 and layer 2
- Have to re-built the image


*Fast Way*
```
COPY requirements.txt .       # Layer 1: Just the list of names (Rarely changes)
RUN pip install pandas numpy  # Layer 2: Installs heavy libraries (Cached!)
COPY . .                      # Layer 3: Copies your code (Changes often)
```

- If anything changes in layer 3 then layer 1 and 2 don't change 

2. **Image**
	- Once built, an image **cannot** be changed
	- Layers make up the image 
	- Docker file makes this 
	- contains os and dependencies for running the application 
	- Template for running the application 

3. **Container**
	- Portable 
	- Scalable 
	- Stateless 
		- doesn't store data locally: offloads data to an external database or filesystem 
	- Designed to be stopped, destroyed and replaced. 

--- 
## Docker Setup 

**Docker Desktop** 

- https://www.docker.com

*Includes everything* 

1. Docker Engine
	- Core background process (Daemon) -> in charge of running containers
	
2. Docker CLI
	- ``` docker``` command-line tool for interacting with engine 

3. Docker Compose
	- Tool for defining and running multiple container applications

4. Kubernetes. 
	- A local single-node cluster 
	- can activate through settings 


**Verifying Installation** 
```
docker --version
docker compose version
```


**List of CLI Commands**

|**Command**|**What it does**|
|---|---|
|`docker compose up -d`|Starts all services in the background (detached mode).|
|`docker compose down`|Stops and **removes** all containers and networks for the project.|
|`docker compose stop`|Stops the containers but does not remove them.|
|`docker compose ps`|Shows the status of all containers in your current project.|
|`docker compose logs -f`|Views live, color-coded logs from all services (great for debugging).|
|`docker compose build`|Rebuilds your images (use this after changing a `Dockerfile`).|
|`docker compose exec [service] bash`|Opens a terminal inside a specific service (e.g., `db` or `backend`).|

---
## Docker File: Creation 

This is just a basic template. There are other instructions "all caps keyword" to customize your Docker File. 

```
# 1. BASE IMAGE
# Every image starts from a base (e.g., Python, Node, Ubuntu, Alpine).
# syntax: FROM <image_name>:<tag>
#image_name = OS
#tag = version of OS 
FROM python:3.9-slim

# 2. WORK DIRECTORY
# Sets the working directory inside the container.
# All subsequent commands happen here.
WORKDIR /app

# 3. COPY DEPENDENCIES
# Copy just the dependency file first to leverage Docker's cache layers.
# syntax: COPY <source_on_machine> <destination_in_container>
COPY requirements.txt .

# 4. INSTALL DEPENDENCIES
# Run the command to install libraries.
RUN pip install -r requirements.txt

# 5. COPY SOURCE CODE
# Copy the rest of your application code into the container.
COPY . .

# 6. EXPOSE PORT (Optional)
# Documents which port the app listens on (informational).
EXPOSE 8080

# 7. STARTUP COMMAND
# The command that runs when the container starts.
# syntax: CMD ["executable", "param1", "param2"]
CMD ["python", "app.py"]
```

--- 

## Docker Compose 

Docker Compose is a file that lets you run multiple containers in succession. 

**Analogy** 
- Docker File -> Single musician player
- Docker Compose -> Orchestrator 

**Details** 
- Docker Compose looks for images (prebuilt results of a docker image)

**Creating a Compose File**

```
# -----------------------------------------------------------------------------
# DOCKER COMPOSE TEMPLATE (Backend API + PostgreSQL Database)
# -----------------------------------------------------------------------------
version: '3.8'  # The standard, stable version of the Compose file format.

services:
  
  # ===========================================================================
  # SERVICE 1: YOUR APPLICATION (The Code)
  # ===========================================================================
  backend-app:
    # BUILD CONTEXT
    # tells Docker: "Look for a Dockerfile in the current directory (.)"
    # If your Dockerfile is in a subfolder, change this to: ./my-app-folder
    build: . 
    
    # CONTAINER NAME
    # Gives the container a specific name so you can find it easily 
    # (e.g., 'docker stop my-research-project' instead of a random ID).
    container_name: my-research-project-app

    # PORTS
    # Maps the container's internal port to your laptop's port.
    # Format: "HOST_PORT:CONTAINER_PORT"
    # Example: Access the app at http://localhost:8080
    ports:
      - "8080:5000"  # (5000 is standard for Flask, 8080 for ASP.NET)

    # ENVIRONMENT VARIABLES
    # Injects configuration into your code (os.environ or IConfiguration).
    # CRITICAL: This is how your app knows how to talk to the DB.
    environment:
      - APP_ENV=development
      # "db" below matches the service name of the database defined on line 45
      - DB_HOST=db 
      - DB_NAME=my_project_db
      - DB_USER=admin
      - DB_PASSWORD=securepassword123

    # VOLUMES (Hot Reloading)
    # Maps your local code folder to the container folder.
    # If you save a file on your laptop, the container sees the change instantly.
    volumes:
      - .:/app

    # DEPENDENCIES
    # Tells Docker: "Don't start this app until the 'db' service is running."
    depends_on:
      - db

  # ===========================================================================
  # SERVICE 2: THE DATABASE (PostgreSQL)
  # ===========================================================================
  db:
    # IMAGE
    # We don't build this. We pull the official image from Docker Hub.
    # "alpine" versions are lightweight (smaller download size).
    image: postgres:15-alpine

    container_name: my-project-postgres

    # RESTART POLICY
    # If the database crashes, Docker will automatically try to start it again.
    restart: always

    # ENVIRONMENT VARIABLES
    # These specific variables tell the Postgres image how to set itself up 
    # the very first time it runs.
    environment:
      POSTGRES_USER: admin
      POSTGRES_PASSWORD: securepassword123
      POSTGRES_DB: my_project_db

    # PORTS
    # Optional: Only needed if you want to connect to the DB using a tool 
    # like pgAdmin or DBeaver from your laptop.
    ports:
      - "5432:5432"

    # VOLUMES (Persistence)
    # This is CRITICAL. Without this, your data is lost when the container stops.
    # This maps the internal Postgres data folder to a named Docker volume.
    volumes:
      - postgres-data:/var/lib/postgresql/data

# =============================================================================
# VOLUME DEFINITIONS
# =============================================================================
volumes:
  # Declares the volume used by the database service above.
  postgres-data:
```


Another Example 

```
services:                              # Line 1: Defines the containers you want to run
  postgres:                            # Line 2: Name of this service (you reference this in commands)
    image: postgres:16                 # Line 3: Use official PostgreSQL v16 image from Docker Hub
    container_name: projects_db        # Line 4: Name the container (shows up in `docker ps`)
    restart: unless-stopped            # Line 5: Auto-restart if container crashes (not if you stop it manually)
    environment:                       # Line 6: Environment variables passed INTO the container
      POSTGRES_USER: ${POSTGRES_USER}         # Line 7: DB username (pulled from .env file)
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD} # Line 8: DB password (pulled from .env file)
      POSTGRES_DB: ${POSTGRES_DB}             # Line 9: Database name to create (pulled from .env file)
    ports:                             # Line 10: Port mapping
      - "5432:5432"                    # Line 11: "host:container" - your machine's 5432 → container's 5432
    volumes:                           # Line 12: Persistent storage
      - postgres_data:/var/lib/postgresql/data  # Line 13: Named volume → where PostgreSQL stores data inside container

volumes:                               # Line 15: Define named volumes
  postgres_data:                       # Line 16: Creates a volume called "postgres_data"

```

--- 

## Persisting Data with Docker

**Note** 
Remember, containers are stateless meaning that data will not persist. Do not get confused with the *writable layer* on top of the image layers which allow you to change and alter the data during run time. 

**Achieving  Persistence** 
In order to persist data, we need to move the data out of the container's writable layer into the host's storage. 

Done through two instructions: 

1. Volumes 
2. Bind Mounts

**Volumes vs Bind Mounts**
Both persist data but they differ on how they are managed and where live on the host machine. TLDR:
- **Volumes** are managed entirely by Docker
- **Bind Mounts** are just a link to a specific folder on your computer

| **Feature**       | **Docker Volumes**                                            | **Bind Mounts**                                          |
| ----------------- | ------------------------------------------------------------- | -------------------------------------------------------- |
| **Management**    | Managed by Docker CLI/API.                                    | Managed by the host OS (you).                            |
| **Host Location** | Stored in Docker's internal area (`/var/lib/docker/volumes`). | Anywhere on your host (e.g., `/Users/dev/project`).      |
| **Portability**   | Highly portable; works the same on Windows, Mac, and Linux.   | Tied to the host's specific directory structure.         |
| **Performance**   | Optimized; higher performance on Mac/Windows.                 | Can be slower on Mac/Windows due to filesystem overhead. |
| **Isolation**     | High; separated from host system files.                       | Low; containers can modify sensitive host files.         |

**Uses Cases**
1. Use Volumes for Production
	- Ex: PostgreSQL Database
	- Managed by Docker 
	- Do not have to worry file paths or permission on different servers 
	
2. Use Bind Mounts for Development
	- When you want the container to see code changes instantly
	- Mounting the project folder into the container
		- can edit a file in your IDE -> containerized app will reloadbecause it is looking at the same physical files


**Image and Volume Management for Docker CLI** 

|**Command**|**What it does**|
|---|---|
|`docker images`|Lists all downloaded or built images on your machine.|
|`docker rmi [image]`|Deletes a specific image to save disk space.|
|`docker volume ls`|Lists all volumes (where your **PostgreSQL** data lives).|
|`docker volume rm [name]`|Deletes a volume. **Warning:** This deletes the actual data.|
|`docker system prune`|The "nuke" command: removes all stopped containers and unused images.|

---
