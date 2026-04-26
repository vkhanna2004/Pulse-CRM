sed -i 's/ENV ASPNETCORE_URLS=http:\/\/+:80//g' infrastructure/Dockerfile.Deals
sed -i 's/EXPOSE 80/EXPOSE 80 8080/g' infrastructure/Dockerfile.Deals

sed -i 's/ENV ASPNETCORE_URLS=http:\/\/+:80//g' infrastructure/Dockerfile.Scoring
sed -i 's/EXPOSE 80/EXPOSE 80 8080/g' infrastructure/Dockerfile.Scoring
