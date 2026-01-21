# Docker Compose Configuration (Commented Version)

This is the original docker-compose.yml with line-by-line explanations.

```yaml
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
