FROM node:20-alpine AS build
WORKDIR /app

ARG VITE_GATEWAY_HTTP_URL=http://localhost:5001
ENV VITE_GATEWAY_HTTP_URL=$VITE_GATEWAY_HTTP_URL

COPY src/WebClient/package*.json ./
RUN npm install

COPY src/WebClient/ ./
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY src/WebClient/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 5173
CMD ["nginx", "-g", "daemon off;"]
