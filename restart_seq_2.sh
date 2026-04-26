nfrastructure/Dockerfile.Gateway .
#!/bin/bash
nfrastructure/Dockerfile.Gateway .
docker stop infrastructure-gateway-1 infrastructure-deals-1 infrastructure-scoring-1 infrastructure-summarization-1 infrastructure-notifications-1
nfrastructure/Dockerfile.Gateway .
docker build -t infrastructure-deals -f infrastructure/Dockerfile.Deals .
nfrastructure/Dockerfile.Gateway .
docker build -t infrastructure-scoring -f infrastructure/Dockerfile.Scoring .
nfrastructure/Dockerfile.Gateway .
docker build -t infrastructure-summarization -f infrastructure/Dockerfile.Summarization .
nfrastructure/Dockerfile.Gateway .
docker build -t infrastructure-notifications -f infrastructure/Dockerfile.Notifications .
docker build -t infrastructure-gateway -f
nfrastructure/Dockerfile.Gateway .
docker compose -f infrastructure/docker-compose.yml up --force-recreate -d gateway deals scoring summarization notifications
