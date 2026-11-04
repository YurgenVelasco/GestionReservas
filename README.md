# Mesa Común — Gestión de reservas

Aplicación web local para gestionar reservas, mesas, pedidos, cocina, despachos y usuarios.

## Requisitos

- Windows
- .NET SDK 10
- SQL Server Express LocalDB con la instancia `MSSQLLocalDB`

## Iniciar

Haz doble clic en `Iniciar-MesaComun.bat` o abre una terminal en esta carpeta y ejecuta:

```powershell
dotnet run --project api/GestionReservas.Api.csproj
```

Cuando el servidor indique que está escuchando, abre [http://localhost:5080](http://localhost:5080). El servidor crea la base `GestionReservas`, sus tablas y las cuentas de demostración al primer inicio.

## Cuentas de demostración

| Rol | Usuario | Contraseña |
|---|---|---|
| Administrador | `admin` | `admin123` |
| Mesero | `mesero` | `mesero123` |
| Cocina | `cocina` | `cocina123` |
| Despacho | `despacho` | `despacho123` |

## Datos y privacidad

Reservas, pedidos, platos, mesas y usuarios se guardan en SQL Server LocalDB mediante la API local. La interfaz no escribe en `localStorage` ni `sessionStorage`. La cookie de sesión es `HttpOnly`; las contraseñas de usuarios se guardan con hash PBKDF2.

La cadena de conexión está en `api/appsettings.json`. El botón **Restablecer datos demo** borra las reservas, pedidos y despachos, elimina cuentas personalizadas y devuelve las mesas al estado disponible. Conserva las cuentas demo y el menú.
