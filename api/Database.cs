using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace GestionReservas.Api;

public sealed class Database(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("RestaurantDb")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:RestaurantDb.");

    private SqlConnection OpenConnection() => new(_connectionString);

    public async Task InitializeAsync()
    {
        // LocalDB permite crear la base desde cero. En un hosting la base ya
        // existe y el usuario no suele tener permiso para crearla.
        if (_connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
            await using var master = new SqlConnection(builder.ConnectionString);
            await master.OpenAsync();
            await using var createDb = master.CreateCommand();
            createDb.CommandText = "IF DB_ID(N'GestionReservas') IS NULL CREATE DATABASE [GestionReservas];";
            await createDb.ExecuteNonQueryAsync();
        }

        await using var connection = OpenConnection();
        await connection.OpenAsync();
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = """
                IF OBJECT_ID(N'dbo.Users', N'U') IS NULL CREATE TABLE dbo.Users (
                    Id nvarchar(50) NOT NULL PRIMARY KEY, Username nvarchar(30) NOT NULL UNIQUE,
                    DisplayName nvarchar(70) NOT NULL, Role nvarchar(20) NOT NULL,
                    PasswordHash nvarchar(250) NOT NULL, IsDemo bit NOT NULL DEFAULT 0,
                    CONSTRAINT CK_Users_Role CHECK (Role IN ('admin','mesero','cocina','despacho')));
                IF OBJECT_ID(N'dbo.Tables', N'U') IS NULL CREATE TABLE dbo.Tables (
                    Id int NOT NULL PRIMARY KEY, Name nvarchar(40) NOT NULL, Seats int NOT NULL,
                    Status nvarchar(20) NOT NULL DEFAULT 'disponible');
                IF OBJECT_ID(N'dbo.Dishes', N'U') IS NULL CREATE TABLE dbo.Dishes (
                    Id nvarchar(30) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL, Price decimal(12,2) NOT NULL);
                IF OBJECT_ID(N'dbo.Reservations', N'U') IS NULL CREATE TABLE dbo.Reservations (
                    Id nvarchar(60) NOT NULL PRIMARY KEY, Name nvarchar(80) NOT NULL, Phone nvarchar(30) NOT NULL DEFAULT '',
                    TableId int NOT NULL, ReservationDate date NOT NULL, ReservationTime time(0) NOT NULL,
                    People int NOT NULL, Status nvarchar(20) NOT NULL, CreatedBy nvarchar(30) NOT NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_Reservations_Tables FOREIGN KEY (TableId) REFERENCES dbo.Tables(Id));
                IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL CREATE TABLE dbo.Orders (
                    Id nvarchar(60) NOT NULL PRIMARY KEY, TableId int NOT NULL, Notes nvarchar(160) NOT NULL DEFAULT '',
                    Status nvarchar(20) NOT NULL, CreatedBy nvarchar(30) NOT NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_Orders_Tables FOREIGN KEY (TableId) REFERENCES dbo.Tables(Id));
                IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL CREATE TABLE dbo.OrderItems (
                    OrderId nvarchar(60) NOT NULL, LineIndex int NOT NULL, DishId nvarchar(30) NOT NULL,
                    Quantity int NOT NULL, Status nvarchar(20) NOT NULL,
                    CONSTRAINT PK_OrderItems PRIMARY KEY (OrderId, LineIndex),
                    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_OrderItems_Dishes FOREIGN KEY (DishId) REFERENCES dbo.Dishes(Id));
                IF OBJECT_ID(N'dbo.Deliveries', N'U') IS NULL CREATE TABLE dbo.Deliveries (
                    Id nvarchar(60) NOT NULL PRIMARY KEY, OrderId nvarchar(60) NOT NULL, TableId int NOT NULL,
                    Status nvarchar(20) NOT NULL, CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_Deliveries_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id),
                    CONSTRAINT FK_Deliveries_Tables FOREIGN KEY (TableId) REFERENCES dbo.Tables(Id));
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Reservations_DateTime' AND object_id=OBJECT_ID(N'dbo.Reservations')) CREATE INDEX IX_Reservations_DateTime ON dbo.Reservations(ReservationDate, ReservationTime, TableId);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Orders_CreatedAt' AND object_id=OBJECT_ID(N'dbo.Orders')) CREATE INDEX IX_Orders_CreatedAt ON dbo.Orders(CreatedAt DESC);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Deliveries_Status' AND object_id=OBJECT_ID(N'dbo.Deliveries')) CREATE INDEX IX_Deliveries_Status ON dbo.Deliveries(Status);
                """;
            await schema.ExecuteNonQueryAsync();
        }
        await SeedAsync(connection);
    }

    private static async Task SeedAsync(SqlConnection connection)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        var seedUsers = new (string Id, string Username, string Name, string Role, string Password)[]
        {
            ("u1", "admin", "Administrador", "admin", "admin123"),
            ("u2", "mesero", "Equipo de sala", "mesero", "mesero123"),
            ("u3", "cocina", "Equipo de cocina", "cocina", "cocina123"),
            ("u4", "despacho", "Equipo de despacho", "despacho", "despacho123")
        };
        foreach (var user in seedUsers)
        {
            await using var command = new SqlCommand("IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username=@username) INSERT dbo.Users(Id,Username,DisplayName,Role,PasswordHash,IsDemo) VALUES(@id,@username,@name,@role,@hash,1);", connection, transaction);
            command.Parameters.AddWithValue("@id", user.Id); command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@name", user.Name); command.Parameters.AddWithValue("@role", user.Role);
            command.Parameters.AddWithValue("@hash", HashPassword(user.Password)); await command.ExecuteNonQueryAsync();
        }
        for (var i = 1; i <= 8; i++)
        {
            await using var command = new SqlCommand("IF NOT EXISTS (SELECT 1 FROM dbo.Tables WHERE Id=@id) INSERT dbo.Tables(Id,Name,Seats,Status) VALUES(@id,@name,@seats,'disponible');", connection, transaction);
            command.Parameters.AddWithValue("@id", i); command.Parameters.AddWithValue("@name", $"Mesa {i}"); command.Parameters.AddWithValue("@seats", i <= 4 ? 2 : 4); await command.ExecuteNonQueryAsync();
        }
        var dishes = new (string Id, string Name, decimal Price)[]
        {
            ("p1", "Arepa de choclo", 18000), ("p2", "Bandeja de la casa", 32000),
            ("p3", "Trucha a la plancha", 36000), ("p4", "Sopa del día", 14000),
            ("p5", "Ensalada fresca", 19000), ("p6", "Limonada natural", 7000),
            ("p7", "Jugo de mora", 8000), ("p8", "Postre de la casa", 10000)
        };
        foreach (var dish in dishes)
        {
            await using var command = new SqlCommand("IF NOT EXISTS (SELECT 1 FROM dbo.Dishes WHERE Id=@id) INSERT dbo.Dishes(Id,Name,Price) VALUES(@id,@name,@price);", connection, transaction);
            command.Parameters.AddWithValue("@id", dish.Id); command.Parameters.AddWithValue("@name", dish.Name); command.Parameters.AddWithValue("@price", dish.Price); await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    public async Task<AppUser?> FindUserAsync(string username)
    {
        await using var connection = OpenConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT TOP(1) Id,Username,DisplayName,Role,PasswordHash,IsDemo FROM dbo.Users WHERE Username=@username;", connection);
        command.Parameters.AddWithValue("@username", username.Trim().ToLowerInvariant()); await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    public async Task<BootstrapData> GetBootstrapAsync(bool includeUsers)
    {
        await using var connection = OpenConnection(); await connection.OpenAsync();
        var tables = new List<RestaurantTable>();
        await using (var cmd = new SqlCommand("SELECT Id,Name,Seats,Status FROM dbo.Tables ORDER BY Id;", connection))
        await using (var reader = await cmd.ExecuteReaderAsync()) while (await reader.ReadAsync()) tables.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3)));
        var dishes = new List<Dish>();
        await using (var cmd = new SqlCommand("SELECT Id,Name,Price FROM dbo.Dishes ORDER BY Name;", connection))
        await using (var reader = await cmd.ExecuteReaderAsync()) while (await reader.ReadAsync()) dishes.Add(new(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2)));
        var reservations = new List<Reservation>();
        await using (var cmd = new SqlCommand("SELECT Id,Name,Phone,TableId,CONVERT(char(10),ReservationDate,23),CONVERT(char(5),ReservationTime,108),People,Status,CreatedBy,CreatedAt FROM dbo.Reservations ORDER BY ReservationDate,ReservationTime;", connection))
        await using (var reader = await cmd.ExecuteReaderAsync()) while (await reader.ReadAsync()) reservations.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetInt32(6), reader.GetString(7), reader.GetString(8), DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc)));
        var orders = new List<Order>();
        await using (var cmd = new SqlCommand("SELECT Id,TableId,Notes,Status,CreatedBy,CreatedAt FROM dbo.Orders ORDER BY CreatedAt DESC;", connection))
        await using (var reader = await cmd.ExecuteReaderAsync()) while (await reader.ReadAsync()) orders.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetString(2), [], reader.GetString(3), reader.GetString(4), DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)));
        var orderMap = orders.ToDictionary(o => o.Id);
        await using (var cmd = new SqlCommand("SELECT OrderId,LineIndex,DishId,Quantity,Status FROM dbo.OrderItems ORDER BY OrderId,LineIndex;", connection))
        await using (var reader = await cmd.ExecuteReaderAsync()) while (await reader.ReadAsync()) if (orderMap.TryGetValue(reader.GetString(0), out var order)) order.Lines.Add(new(reader.GetString(2), reader.GetInt32(3), reader.GetString(4)));
        var deliveries = new List<Delivery>();
        await using (var cmd = new SqlCommand("SELECT Id,OrderId,TableId,Status,CreatedAt FROM dbo.Deliveries ORDER BY CreatedAt DESC;", connection))
        await using (var reader = await cmd.ExecuteReaderAsync()) while (await reader.ReadAsync()) deliveries.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)));
        var users = new List<UserSummary>();
        if (includeUsers)
        {
            await using var cmd = new SqlCommand("SELECT Id,Username,DisplayName,Role FROM dbo.Users ORDER BY DisplayName;", connection);
            await using var reader = await cmd.ExecuteReaderAsync(); while (await reader.ReadAsync()) users.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }
        return new(1, tables, dishes, reservations, orders, deliveries, users);
    }

    public async Task<Reservation> CreateReservationAsync(ReservationInput input, string creator)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 80 || input.Phone?.Length > 30 || input.Date.Date < DateTime.Today || input.People < 1) throw new InvalidOperationException("Revisa los datos de la reserva.");
        await using var connection = OpenConnection(); await connection.OpenAsync(); await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        int seats;
        await using (var table = new SqlCommand("SELECT Seats FROM dbo.Tables WHERE Id=@tableId;", connection, tx)) { table.Parameters.AddWithValue("@tableId", input.TableId); var result = await table.ExecuteScalarAsync(); if (result is null) throw new InvalidOperationException("La mesa seleccionada no existe."); seats = Convert.ToInt32(result); }
        if (input.People > seats) throw new InvalidOperationException("La cantidad de personas supera la capacidad de la mesa.");
        await using (var check = new SqlCommand("SELECT COUNT(1) FROM dbo.Reservations WITH (UPDLOCK,HOLDLOCK) WHERE TableId=@table AND ReservationDate=@date AND ReservationTime=@time AND Status<>'cancelada';", connection, tx))
        {
            check.Parameters.AddWithValue("@table", input.TableId); check.Parameters.AddWithValue("@date", input.Date.Date); check.Parameters.AddWithValue("@time", input.Time);
            if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0) throw new InvalidOperationException("Esa mesa ya tiene una reserva en ese horario.");
        }
        var item = new Reservation(NewId("res"), input.Name.Trim(), input.Phone?.Trim() ?? "", input.TableId, input.Date.ToString("yyyy-MM-dd"), input.Time.ToString(@"hh\:mm"), input.People, "confirmada", creator, DateTime.UtcNow);
        await using (var insert = new SqlCommand("INSERT dbo.Reservations(Id,Name,Phone,TableId,ReservationDate,ReservationTime,People,Status,CreatedBy) VALUES(@id,@name,@phone,@table,@date,@time,@people,'confirmada',@creator);", connection, tx))
        {
            insert.Parameters.AddWithValue("@id", item.Id); insert.Parameters.AddWithValue("@name", item.Name); insert.Parameters.AddWithValue("@phone", item.Phone); insert.Parameters.AddWithValue("@table", item.TableId); insert.Parameters.AddWithValue("@date", input.Date.Date); insert.Parameters.AddWithValue("@time", input.Time); insert.Parameters.AddWithValue("@people", item.People); insert.Parameters.AddWithValue("@creator", creator); await insert.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync(); return item;
    }

    public async Task UpdateReservationAsync(string id, string status)
    {
        if (status is not ("confirmada" or "cancelada" or "atendida")) throw new InvalidOperationException("Estado de reserva no válido.");
        await using var connection = OpenConnection(); await connection.OpenAsync(); await using var cmd = new SqlCommand("UPDATE dbo.Reservations SET Status=@status WHERE Id=@id;", connection); cmd.Parameters.AddWithValue("@status", status); cmd.Parameters.AddWithValue("@id", id); if (await cmd.ExecuteNonQueryAsync() == 0) throw new KeyNotFoundException("No se encontró la reserva.");
    }

    public async Task<string> CreateOrderAsync(OrderInput input, string creator)
    {
        var lines = input.Lines?.Where(x => x.Quantity > 0).ToList() ?? [];
        if (lines.Count == 0 || lines.Any(x => x.Quantity > 20)) throw new InvalidOperationException("Selecciona platos con cantidades entre 1 y 20.");
        var id = NewId("ord"); await using var connection = OpenConnection(); await connection.OpenAsync(); await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
        await using (var check = new SqlCommand("SELECT COUNT(1) FROM dbo.Tables WHERE Id=@id;", connection, tx)) { check.Parameters.AddWithValue("@id", input.TableId); if (Convert.ToInt32(await check.ExecuteScalarAsync()) == 0) throw new InvalidOperationException("La mesa no existe."); }
        foreach (var line in lines)
        {
            await using var dish = new SqlCommand("SELECT COUNT(1) FROM dbo.Dishes WHERE Id=@id;", connection, tx); dish.Parameters.AddWithValue("@id", line.DishId); if (Convert.ToInt32(await dish.ExecuteScalarAsync()) == 0) throw new InvalidOperationException("Uno de los platos seleccionados no existe.");
        }
        await using (var cmd = new SqlCommand("INSERT dbo.Orders(Id,TableId,Notes,Status,CreatedBy) VALUES(@id,@table,@notes,'pendiente',@creator); UPDATE dbo.Tables SET Status='ocupada' WHERE Id=@table;", connection, tx))
        { cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@table", input.TableId); cmd.Parameters.AddWithValue("@notes", input.Notes?.Trim() ?? ""); cmd.Parameters.AddWithValue("@creator", creator); await cmd.ExecuteNonQueryAsync(); }
        for (var index = 0; index < lines.Count; index++)
        {
            await using var cmd = new SqlCommand("INSERT dbo.OrderItems(OrderId,LineIndex,DishId,Quantity,Status) VALUES(@order,@index,@dish,@quantity,'pendiente');", connection, tx); cmd.Parameters.AddWithValue("@order", id); cmd.Parameters.AddWithValue("@index", index); cmd.Parameters.AddWithValue("@dish", lines[index].DishId); cmd.Parameters.AddWithValue("@quantity", lines[index].Quantity); await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync(); return id;
    }

    public async Task UpdateOrderLineAsync(string orderId, int lineIndex, string status)
    {
        if (status is not ("pendiente" or "preparacion" or "listo")) throw new InvalidOperationException("Estado de plato no válido.");
        await using var connection = OpenConnection(); await connection.OpenAsync(); await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
        await using (var update = new SqlCommand("UPDATE dbo.OrderItems SET Status=@status WHERE OrderId=@order AND LineIndex=@line;", connection, tx)) { update.Parameters.AddWithValue("@status", status); update.Parameters.AddWithValue("@order", orderId); update.Parameters.AddWithValue("@line", lineIndex); if (await update.ExecuteNonQueryAsync() == 0) throw new KeyNotFoundException("No se encontró el plato del pedido."); }
        await using (var updateOrder = new SqlCommand("UPDATE dbo.Orders SET Status=CASE WHEN NOT EXISTS(SELECT 1 FROM dbo.OrderItems WHERE OrderId=@order AND Status<>'listo') THEN 'listo' WHEN EXISTS(SELECT 1 FROM dbo.OrderItems WHERE OrderId=@order AND Status='preparacion') THEN 'preparacion' ELSE 'pendiente' END WHERE Id=@order;", connection, tx)) { updateOrder.Parameters.AddWithValue("@order", orderId); await updateOrder.ExecuteNonQueryAsync(); }
        await tx.CommitAsync();
    }

    public async Task<string> CreateDeliveryAsync(string orderId)
    {
        await using var connection = OpenConnection(); await connection.OpenAsync(); await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        int tableId;
        await using (var check = new SqlCommand("SELECT TableId FROM dbo.Orders WHERE Id=@order AND EXISTS(SELECT 1 FROM dbo.OrderItems WHERE OrderId=@order) AND NOT EXISTS(SELECT 1 FROM dbo.OrderItems WHERE OrderId=@order AND Status<>'listo');", connection, tx)) { check.Parameters.AddWithValue("@order", orderId); var value = await check.ExecuteScalarAsync(); if (value is null) throw new InvalidOperationException("Todos los platos del pedido deben estar listos."); tableId = Convert.ToInt32(value); }
        await using (var existing = new SqlCommand("SELECT COUNT(1) FROM dbo.Deliveries WHERE OrderId=@order AND Status<>'entregado';", connection, tx)) { existing.Parameters.AddWithValue("@order", orderId); if (Convert.ToInt32(await existing.ExecuteScalarAsync()) > 0) throw new InvalidOperationException("Este pedido ya tiene un despacho activo."); }
        var id = NewId("del"); await using (var insert = new SqlCommand("INSERT dbo.Deliveries(Id,OrderId,TableId,Status) VALUES(@id,@order,@table,'pendiente');", connection, tx)) { insert.Parameters.AddWithValue("@id", id); insert.Parameters.AddWithValue("@order", orderId); insert.Parameters.AddWithValue("@table", tableId); await insert.ExecuteNonQueryAsync(); }
        await tx.CommitAsync(); return id;
    }

    public async Task UpdateDeliveryAsync(string id, string status)
    {
        if (status is not ("pendiente" or "ruta" or "entregado")) throw new InvalidOperationException("Estado de despacho no válido.");
        await using var connection = OpenConnection(); await connection.OpenAsync(); await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
        int tableId; string orderId;
        await using (var update = new SqlCommand("UPDATE dbo.Deliveries SET Status=@status OUTPUT inserted.TableId,inserted.OrderId WHERE Id=@id;", connection, tx)) { update.Parameters.AddWithValue("@status", status); update.Parameters.AddWithValue("@id", id); await using var reader = await update.ExecuteReaderAsync(); if (!await reader.ReadAsync()) throw new KeyNotFoundException("No se encontró el despacho."); tableId = reader.GetInt32(0); orderId = reader.GetString(1); }
        if (status == "entregado")
        {
            await using var free = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.Deliveries WHERE OrderId=@order AND Id<>@id AND Status<>'entregado') AND NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE TableId=@table AND Id<>@order AND Status<>'listo') UPDATE dbo.Tables SET Status='disponible' WHERE Id=@table;", connection, tx); free.Parameters.AddWithValue("@order", orderId); free.Parameters.AddWithValue("@id", id); free.Parameters.AddWithValue("@table", tableId); await free.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task<UserSummary> AddUserAsync(NewUserInput input)
    {
        var username = input.Username.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-z0-9._-]{3,30}$") || input.Name.Trim().Length is < 1 or > 70 || input.Password.Length < 10 || input.Role is not ("admin" or "mesero" or "cocina" or "despacho")) throw new InvalidOperationException("Completa los campos. La contraseña debe tener al menos 10 caracteres.");
        var user = new UserSummary(NewId("usr"), username, input.Name.Trim(), input.Role);
        await using var connection = OpenConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand("INSERT dbo.Users(Id,Username,DisplayName,Role,PasswordHash,IsDemo) VALUES(@id,@username,@name,@role,@hash,0);", connection);
        command.Parameters.AddWithValue("@id", user.Id); command.Parameters.AddWithValue("@username", user.Username); command.Parameters.AddWithValue("@name", user.Name); command.Parameters.AddWithValue("@role", user.Role); command.Parameters.AddWithValue("@hash", HashPassword(input.Password));
        try { await command.ExecuteNonQueryAsync(); } catch (SqlException ex) when (ex.Number is 2601 or 2627) { throw new InvalidOperationException("Ese nombre de usuario ya está registrado."); }
        return user;
    }

    public async Task RemoveUserAsync(string id, string currentUserId)
    {
        if (id == currentUserId) throw new InvalidOperationException("No puedes eliminar tu propia cuenta.");
        await using var connection = OpenConnection(); await connection.OpenAsync(); await using var command = new SqlCommand("DELETE dbo.Users WHERE Id=@id AND IsDemo=0;", connection); command.Parameters.AddWithValue("@id", id); if (await command.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("No se puede eliminar esta cuenta demo o el usuario no existe.");
    }

    public async Task ResetDemoAsync()
    {
        await using var connection = OpenConnection(); await connection.OpenAsync(); await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = new SqlCommand("DELETE dbo.Deliveries; DELETE dbo.OrderItems; DELETE dbo.Orders; DELETE dbo.Reservations; UPDATE dbo.Tables SET Status='disponible'; DELETE dbo.Users WHERE IsDemo=0;", connection, tx); await command.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }

    private static AppUser ReadUser(SqlDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetBoolean(5));
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16); var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public static bool VerifyPassword(string password, string encoded)
    {
        var parts = encoded.Split('$'); if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations)) return false;
        try { var salt = Convert.FromBase64String(parts[2]); var expected = Convert.FromBase64String(parts[3]); var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length); return CryptographicOperations.FixedTimeEquals(actual, expected); } catch (FormatException) { return false; }
    }
    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}

public sealed record AppUser(string Id, string Username, string Name, string Role, string PasswordHash, bool IsDemo);
public sealed record UserSummary(string Id, string Username, string Name, string Role);
public sealed record RestaurantTable(int Id, string Name, int Seats, string Status);
public sealed record Dish(string Id, string Name, decimal Price);
public sealed record Reservation(string Id, string Name, string Phone, int TableId, string Date, string Time, int People, string Status, string CreatedBy, DateTime CreatedAt);
public sealed record OrderLine(string DishId, int Quantity, string Status);
public sealed class Order(string id, int tableId, string notes, List<OrderLine> lines, string status, string createdBy, DateTime createdAt)
{
    public string Id { get; } = id; public int TableId { get; } = tableId; public string Notes { get; } = notes;
    public List<OrderLine> Lines { get; } = lines; public string Status { get; set; } = status; public string CreatedBy { get; } = createdBy; public DateTime CreatedAt { get; } = createdAt;
}
public sealed record Delivery(string Id, string OrderId, int TableId, string Status, DateTime CreatedAt);
public sealed record BootstrapData(int Version, List<RestaurantTable> Tables, List<Dish> Dishes, List<Reservation> Reservations, List<Order> Orders, List<Delivery> Deliveries, List<UserSummary> Users);
public sealed record ReservationInput(string Name, string? Phone, int TableId, DateTime Date, TimeSpan Time, int People);
public sealed record OrderLineInput(string DishId, int Quantity);
public sealed record OrderInput(int TableId, string? Notes, List<OrderLineInput>? Lines);
public sealed record NewUserInput(string Username, string Name, string Role, string Password);
public sealed record LoginInput(string Username, string Password);
public sealed record StatusInput(string Status);
