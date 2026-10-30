# Reads JWT_SECRET, POSTGRES_* and ADMIN_* from the TCGBackend .env, cloned next to this one.
-include ../TCGBackend/.env
POSTGRES_CONNECTION ?= Host=localhost;Database=users;Username=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD)
export POSTGRES_CONNECTION JWT_SECRET ADMIN_EMAIL ADMIN_PASSWORD

.PHONY: run test

run:
	dotnet run --project src/UsersService

test:
	dotnet test
