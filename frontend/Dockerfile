FROM node:24.21.0-bookworm-slim
WORKDIR /workspace/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
COPY demo-data/catalog.json /workspace/demo-data/catalog.json
EXPOSE 5173
# Reconcile the Linux-only dependency volume after lockfile changes as well.
CMD ["sh", "-c", "npm ci && exec npm run dev -- --host 0.0.0.0 --port 5173 --strictPort"]
