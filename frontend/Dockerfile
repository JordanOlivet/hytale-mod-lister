FROM node:22-alpine AS build
ARG VERSION=0.0.0-dev
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm pkg set version=${VERSION}
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/build /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
