# users-service

C# (.NET 8) + PostgreSQL. Registro, login y perfil del cliente. Implementa
`proto/users.proto` en el puerto gRPC 50054.

- Contraseñas con hash **bcrypt**, nunca en texto plano.
- Login y registro devuelven un **JWT** firmado con `JWT_SECRET`.
- El BFF verifica ese JWT por su cuenta (misma clave), sin llamar a este servicio.

## Estructura

```
src/UsersService/
  Program.cs            arranque: Kestrel HTTP/2, gRPC, health check y reflection
  UsersConfig.cs        variables de entorno (falla al arrancar si falta algo)
  Grpc/                 handlers de los 4 RPCs, validaciones y conversión entidad ↔ proto
  Auth/TokenService.cs  emite el JWT
  Data/                 EF Core: tabla users, creación de la base y admin inicial
tests/UsersService.Tests/   xUnit con EF Core en memoria (no necesita PostgreSQL)
```

A diferencia de catalog-service, el código gRPC **no se sube al repo**: el paquete
`Grpc.Tools` lo genera en cada `dotnet build` a partir de `proto/users.proto`. Por eso
el Dockerfile se construye desde la raíz del repo (necesita la carpeta `proto/`).

## Correr en local

Requisitos: .NET 8 SDK y Docker.

```bash
# desde la raíz del repo (una vez): crea .env y pon un JWT_SECRET
cp .env.example .env
openssl rand -base64 48   # copia el resultado en JWT_SECRET

docker compose up -d postgres

# desde services/users-service (lee el .env de la raíz)
make run    # levanta el servidor en :50054
make test
```

O todo con Docker, desde la raíz:

```bash
docker compose up -d --build users-service
```

Al arrancar, el servicio crea la base `users` y la tabla si no existen, y el usuario
admin de `ADMIN_EMAIL` / `ADMIN_PASSWORD` si no está registrado.

> **Ojo:** no hay migraciones todavía (`EnsureCreated`). Si cambia el modelo de
> `UserEntity`, en local hay que borrar la base: `docker compose exec postgres dropdb -U tcg users`.

## Probar con grpcurl

```bash
grpcurl -plaintext localhost:50054 list

grpcurl -plaintext -d '{"email":"ash@example.com","password":"pikachu123","name":"Ash Ketchum"}' \
  localhost:50054 tcgstore.users.UsersService/Register

grpcurl -plaintext -d '{"email":"ash@example.com","password":"pikachu123"}' \
  localhost:50054 tcgstore.users.UsersService/Login

# usa el id que devolvió Register/Login
grpcurl -plaintext -d '{"user_id":"usr_...","shipping_address":{"recipient_name":"Ash Ketchum",
  "street":"Route 1 #10","city":"Pallet Town","country":"CL"}}' \
  localhost:50054 tcgstore.users.UsersService/UpdateProfile
```

Para ver qué trae el token, pega el `access_token` en <https://jwt.io>.

## Variables de entorno

| Variable | Default | |
|---|---|---|
| `POSTGRES_CONNECTION` | — (obligatoria) | `Host=localhost;Database=users;Username=tcg;Password=tcg` |
| `JWT_SECRET` | — (obligatoria) | mínimo 32 bytes; la misma que usa el BFF |
| `GRPC_PORT` | `50054` | |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | vacías | si están, crea ese admin al arrancar |

## Reglas que conviene conocer

- **Email**: se guarda en minúsculas y sin espacios; `Ash@Example.com` y
  `ash@example.com` son la misma cuenta.
- **Contraseña**: mínimo 8 caracteres y máximo 72 bytes (bcrypt ignora lo que
  sobra, así que se rechaza en vez de truncarla en silencio).
- **Login fallido**: siempre `UNAUTHENTICATED` con el mismo mensaje, sea email
  desconocido o contraseña incorrecta, para no revelar qué emails existen.
- **Register** siempre crea `customer`. Los admins solo salen de `ADMIN_EMAIL`.
- **UpdateProfile es parcial**: un `name` vacío o sin `shipping_address` deja el
  valor actual. Si viene dirección, exige `recipient_name`, `street`, `city` y `country`.
- Los ids tienen la forma `usr_` + 12 caracteres hex (`usr_75c1e6843159`).

## Notas para el BFF

- JWT `HS256`, expira en 24 h. Claims: `sub` (id), `email`, `role` (`customer` |
  `admin`), `iat`, `nbf`, `exp`. `expires_at` de la respuesta es igual a `exp`.
- La clave es `JWT_SECRET` tal cual (como texto UTF-8). En NestJS:
  `JwtModule.register({ secret: process.env.JWT_SECRET, verifyOptions: { algorithms: ['HS256'] } })`.
- `expires_at` es `int64`: con `@grpc/proto-loader` conviene `longs: Number`.
