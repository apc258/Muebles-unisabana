# Etapa de construcción
FROM node:20-alpine AS build

WORKDIR /app

ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL

COPY package*.json ./

RUN npm install

COPY . .

RUN npm run build

# Etapa de producción
FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]