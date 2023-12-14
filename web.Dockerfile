FROM node:18-alpine as build
WORKDIR /workspace
ENV NODE_ENV production
COPY ./src/WildGoose.Web/ /workspace/
COPY ./src/WildGoose.Web/tsconfig.build.json /workspace/tsconfig.json
RUN yarn add typescript --dev 
RUN yarn install
RUN yarn run build

FROM nginx:alpine3.18
WORKDIR /app
COPY --from=build /app/dist .
RUN gzip -k /app/*
COPY ./src/WildGoose.Web/nginx.conf /etc/nginx/nginx.conf
