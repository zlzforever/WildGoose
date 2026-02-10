FROM node:20-alpine as build
WORKDIR /workspace
ENV NODE_ENV production
COPY ./src/WildGoose.Web/ /workspace/
COPY ./src/WildGoose.Web/tsconfig.build.json /workspace/tsconfig.json
RUN yarn install
RUN yarn run build

FROM nginx:alpine3.18
WORKDIR /app
COPY --from=build /workspace/dist .
COPY ./src/WildGoose.Web/nginx.conf /etc/nginx/nginx.conf
COPY ./src/WildGoose.Web/docker-entrypoint.sh /docker-entrypoint.d/90-replace-base-path.sh
RUN chmod +x /docker-entrypoint.d/90-replace-base-path.sh
ENV PATH_BASE='/'
# ENTRYPOINT ["/app/docker-entrypoint.sh"]
