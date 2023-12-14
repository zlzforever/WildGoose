FROM nodejs:18 as base
WORKDIR /app
COPY ./src/WildGoose.Web/package.json /app
RUN npm install

FROM base AS build
WORKDIR /app
COPY ./src/WildGoose.Web/ /app/
RUN npm run build

FROM nginx as final
WORKDIR /app
COPY --from=build /app/dist .
RUN gzip -k /app/*
COPY nginx.conf /etc/nginx/nginx.conf
