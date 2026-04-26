#!/bin/bash
docker stop infrastructure-deals-1 infrastructure-scoring-1 infrastructure-summarization-1
# Build sequentially to not crush memory
docker build -t infrastructure-deals -f infrastructure/Dockerfile.Deals .
docker build -t infrastructure-scoring -f infrastructure/Dockerfile.Scoring .
docker build -t infrastructure-summarization -f infrastructure/Dockerfile.Summarization .
docker compose -f infrastructure/docker-compose.yml up -d deals scoring summarization
