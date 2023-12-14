FROM node:18.19.0-alpine as base
WORKDIR /app
ENV NODE_ENV production
COPY ./src/WildGoose.Web/package.json /app
RUN npm install

FROM base AS build
WORKDIR /app
COPY ./src/WildGoose.Web/ /app/
RUN npm run build

FROM nginx:alpine3.18 as final
WORKDIR /app
COPY --from=build /app/dist .
RUN gzip -k /app/*
COPY ./src/WildGoose.Web/nginx.conf /etc/nginx/nginx.conf
