-- Runs once on first initialization of the postgres-data volume
-- (mounted at /docker-entrypoint-initdb.d by docker-compose.yml).
CREATE DATABASE accounts;
CREATE DATABASE loans;
