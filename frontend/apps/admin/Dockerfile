# Build context: ./frontend (the npm workspace root).
FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json tsconfig.base.json ./
COPY apps/admin/package.json apps/admin/
COPY packages/api-client/package.json packages/api-client/
RUN npm ci

COPY apps/admin/ apps/admin/
COPY packages/api-client/ packages/api-client/
RUN npm run build -w @cafecr/admin

FROM nginx:1.27-alpine AS final
COPY apps/admin/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/apps/admin/dist /usr/share/nginx/html
EXPOSE 80
