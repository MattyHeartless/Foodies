# Build from the repository root:
# docker build -f frontend/Dockerfile -t foodies-web:local .
FROM node:24-bookworm-slim AS dependencies
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

FROM node:24-bookworm-slim AS build
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1 \
    FOODIES_OUTPUT=standalone \
    LOCAL_CANONICAL_ORIGIN=""
ARG API_ORIGIN=http://api:8080
ENV API_ORIGIN=$API_ORIGIN
COPY --from=dependencies /app/node_modules ./node_modules
COPY frontend/ ./
RUN npm run build

FROM node:24-bookworm-slim AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static
COPY --from=build --chown=node:node /app/public ./public
COPY --chown=node:node frontend/scripts/container-health.mjs ./container-health.mjs
USER node
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=8s --start-period=20s --retries=3 \
    CMD ["node", "container-health.mjs"]
CMD ["node", "server.js"]
