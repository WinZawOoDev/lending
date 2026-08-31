# syntax=docker/dockerfile:1
FROM node:24-alpine AS development
WORKDIR /app
COPY package*.json ./
ENV NPM_CONFIG_FETCH_RETRIES=5 \
    NPM_CONFIG_FETCH_RETRY_MAXTIMEOUT=120000 \
    NPM_CONFIG_FETCH_TIMEOUT=600000 \
    NPM_CONFIG_MAXSOCKETS=4
RUN npm ci
COPY . .
ENV NODE_ENV=development
EXPOSE 3000
CMD ["npm", "run", "start:dev"]

FROM development AS db-setup
CMD ["npx", "prisma", "db", "init"]
