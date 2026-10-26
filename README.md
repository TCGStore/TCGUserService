# users-service

C# (.NET) + PostgreSQL.

Registro, login y perfil del cliente (nombre, dirección de envío, historial).

- Contraseñas con hash bcrypt, nunca en texto plano.
- Al hacer login genera un JWT y lo devuelve al BFF por gRPC.
- El BFF verifica la firma del JWT sin llamar a este servicio: ambos comparten
  la clave `JWT_SECRET` (Secret de Kubernetes).
