# Build context: ./frontend (the npm workspace root). One image serves all three apps.
FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json tsconfig.base.json ./
COPY apps/admin/package.json apps/admin/
COPY apps/pos/package.json apps/pos/
COPY apps/customer/package.json apps/customer/
COPY packages/api-client/package.json packages/api-client/
COPY packages/ui/package.json packages/ui/
RUN npm ci

COPY apps/ apps/
COPY packages/ packages/
RUN npm run build -w @cafecr/admin -w @cafecr/pos -w @cafecr/customer

FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/apps/customer/dist /usr/share/nginx/html
COPY --from=build /src/apps/admin/dist /usr/share/nginx/html/admin
COPY --from=build /src/apps/pos/dist /usr/share/nginx/html/pos
EXPOSE 80
