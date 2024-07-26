FROM node:18-alpine as build
WORKDIR /workspace
ENV NODE_ENV production
COPY ./src/WildGoose.Web/ /workspace/
COPY ./src/WildGoose.Web/tsconfig.build.json /workspace/tsconfig.json
RUN yarn install --production=false
RUN yarn run build

FROM nginx:alpine3.18
WORKDIR /app
COPY --from=build /workspace/dist .
RUN gzip -k /app/*
COPY ./src/WildGoose.Web/nginx.conf /etc/nginx/nginx.conf
COPY ./src/WildGoose.Web/docker-entrypoint.sh /app/
RUN chmod +x /app/docker-entrypoint.sh
ENV BASE_PATH='/'
# ENTRYPOINT ["/app/docker-entrypoint.sh"]
