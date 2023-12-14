FROM node:18.19.0-alpine as base
WORKDIR /app
ENV NODE_ENV production
RUN npm install -g typescript 
COPY ./src/WildGoose.Web/ /app/
RUN npm install
RUN npm run build

FROM nginx:alpine3.18 as final
WORKDIR /app
COPY --from=build /app/dist .
RUN gzip -k /app/*
COPY ./src/WildGoose.Web/nginx.conf /etc/nginx/nginx.conf
