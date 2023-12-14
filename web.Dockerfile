FROM node:18.19.0-alpine as build
WORKDIR /workspace
ENV NODE_ENV production
RUN npm install -g typescript 
COPY ./src/WildGoose.Web/ /workspace/
COPY ./src/WildGoose.Web/tsconfig.build.json /workspace/tsconfig.json
RUN npm install @types/react
RUN npm install && ls -l
RUN npm run build

FROM nginx:alpine3.18
WORKDIR /app
COPY --from=build /app/dist .
RUN gzip -k /app/*
COPY ./src/WildGoose.Web/nginx.conf /etc/nginx/nginx.conf
