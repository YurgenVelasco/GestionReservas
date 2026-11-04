/* =====================================================================
   MESA COMUN - Script de insercion de datos de prueba (volumen)
   ---------------------------------------------------------------------
   Deja al menos 10 registros en CADA tabla:
     Users ........ 14 (4 demo originales + 10 nuevos)
     Tables ....... 12 (8 originales + 4 nuevos)
     Dishes ....... 12 (8 originales + 4 nuevos)
     Reservations . 12
     Orders ....... 14
     OrderItems ... 31
     Deliveries ... 10

   La API los lee con GET /api/bootstrap, asi que aparecen en el
   frontend al refrescar la pagina (F5).

   EJECUTAR:
     sqlcmd -S "(localdb)\MSSQLLocalDB" -d GestionReservas -f 65001 -i insertar-datos-prueba.sql

   USUARIOS NUEVOS: todos usan la contrasena  Demo12345!
     demo.laura (mesero) | demo.carlos (cocina) | demo.daniela (despacho)
     demo.jorge (mesero) | demo.valentina (mesero) | demo.andres (admin)
     demo.monica (cocina) | demo.felipe (despacho) | demo.sofia (mesero)
     demo.camilo (admin)

   NOTAS:
   - Se puede ejecutar varias veces: borra y recrea solo los registros
     con prefijo res_demo / ord_demo / del_demo / usr_demo / demo.*
   - Mesas y platos nuevos se insertan solo si no existen.
   - Recalcula el estado (disponible/ocupada) de las mesas 1 a 12
     segun los pedidos realmente existentes.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ================================================================
       1. LIMPIEZA DE EJECUCIONES ANTERIORES
       (siempre hijos antes que padres por las llaves foraneas)
       ================================================================ */
    DELETE FROM dbo.Deliveries   WHERE Id      LIKE 'del_demo%';
    DELETE FROM dbo.OrderItems   WHERE OrderId LIKE 'ord_demo%';
    DELETE FROM dbo.Orders       WHERE Id      LIKE 'ord_demo%';
    DELETE FROM dbo.Reservations WHERE Id      LIKE 'res_demo%';
    DELETE FROM dbo.Users        WHERE Username LIKE 'demo.%';

    /* ================================================================
       2. FECHAS DINAMICAS (siempre vigentes)
       ================================================================ */
    DECLARE @hoy    date = CAST(GETDATE() AS date);
    DECLARE @manana date = DATEADD(DAY, 1, CAST(GETDATE() AS date));
    DECLARE @mas2   date = DATEADD(DAY, 2, CAST(GETDATE() AS date));
    DECLARE @mas3   date = DATEADD(DAY, 3, CAST(GETDATE() AS date));

    /* ================================================================
       3. MESAS (Ids 9 a 12; las 1-8 ya las crea la API)
       ================================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.Tables WHERE Id =  9) INSERT dbo.Tables (Id, Name, Seats, Status) VALUES ( 9, 'Mesa 9',  2, 'disponible');
    IF NOT EXISTS (SELECT 1 FROM dbo.Tables WHERE Id = 10) INSERT dbo.Tables (Id, Name, Seats, Status) VALUES (10, 'Mesa 10', 2, 'disponible');
    IF NOT EXISTS (SELECT 1 FROM dbo.Tables WHERE Id = 11) INSERT dbo.Tables (Id, Name, Seats, Status) VALUES (11, 'Mesa 11', 4, 'disponible');
    IF NOT EXISTS (SELECT 1 FROM dbo.Tables WHERE Id = 12) INSERT dbo.Tables (Id, Name, Seats, Status) VALUES (12, 'Mesa 12', 6, 'disponible');

    /* ================================================================
       4. PLATOS (Ids p9 a p12; p1-p8 ya los crea la API)
       ================================================================ */
    IF NOT EXISTS (SELECT 1 FROM dbo.Dishes WHERE Id = 'p9')  INSERT dbo.Dishes (Id, Name, Price) VALUES ('p9',  'Ajiaco santafereño', 28000);
    IF NOT EXISTS (SELECT 1 FROM dbo.Dishes WHERE Id = 'p10') INSERT dbo.Dishes (Id, Name, Price) VALUES ('p10', 'Churrasco',          42000);
    IF NOT EXISTS (SELECT 1 FROM dbo.Dishes WHERE Id = 'p11') INSERT dbo.Dishes (Id, Name, Price) VALUES ('p11', 'Café de la casa',     5000);
    IF NOT EXISTS (SELECT 1 FROM dbo.Dishes WHERE Id = 'p12') INSERT dbo.Dishes (Id, Name, Price) VALUES ('p12', 'Flan de caramelo',   12000);

    /* ================================================================
       5. USUARIOS (10 nuevos, contrasena Demo12345!)
       PasswordHash = pbkdf2-sha256$iteraciones$salt$hash
       ================================================================ */
    INSERT dbo.Users (Id, Username, DisplayName, Role, PasswordHash, IsDemo) VALUES
        ('usr_demo_400001', 'demo.laura',     'Laura Restrepo',  'mesero',   'pbkdf2-sha256$210000$5tja+PEqHIZDYXQl6wbEvA==$U4lFEDzT1hkU3INOxywZsVfnSZbRZUx8NYUuVVNZqZA=', 1),
        ('usr_demo_400002', 'demo.carlos',    'Carlos Mena',     'cocina',   'pbkdf2-sha256$210000$NhakzpAiNBHh95Qm/ZjymQ==$tCEjvUcC5Hp+niHudYhnNGd5sCPniIKhOCwIjMJY8nU=', 1),
        ('usr_demo_400003', 'demo.daniela',   'Daniela Cruz',    'despacho', 'pbkdf2-sha256$210000$4ABSdPL6dd3lBexKc18jfQ==$6uLpa1LM676d+1EJJ8byJ6DF7hJs8pAq9brZ0yIigHY=', 1),
        ('usr_demo_400004', 'demo.jorge',     'Jorge Salazar',   'mesero',   'pbkdf2-sha256$210000$vQHJxpkVVCDmIBzm4ayB2Q==$88OIrOK5z9MN7TUEtcFOcBqZ5YyaPQKIaAQhtk+BAKU=', 1),
        ('usr_demo_400005', 'demo.valentina', 'Valentina Rios',  'mesero',   'pbkdf2-sha256$210000$FLJnT5nxTShLnc/WezuS3A==$yRFo3B9/VrMg09s0EUACa8N2xVFVo0YNRlOXQC/qC+o=', 1),
        ('usr_demo_400006', 'demo.andres',    'Andres Pardo',    'admin',    'pbkdf2-sha256$210000$PfXJpIgSpqOlI+Lx/dqbwA==$he+Smi2pipQrWatOMjPR6s3jK1bVR8/Kp3iY4RHJbvE=', 1),
        ('usr_demo_400007', 'demo.monica',    'Monica Gil',      'cocina',   'pbkdf2-sha256$210000$rjkB7MOkkJghPC3VWcQVoA==$oEK1DpXwOw88wALJcBBNFOvez6Si/Trxck4XGaKV8Kw=', 1),
        ('usr_demo_400008', 'demo.felipe',    'Felipe Torres',   'despacho', 'pbkdf2-sha256$210000$egqYgQHdLafmBpkqiCue0g==$i/AaxG/csiZFzV8iFXddhocc4G80YhDRXy5pz2JOF5M=', 1),
        ('usr_demo_400009', 'demo.sofia',     'Sofia Herrera',   'mesero',   'pbkdf2-sha256$210000$519nTtSEVOqvHexEhrMlgg==$qMmhpSz+m3D84LgYAG6Zh/+HzbSCbVwz+S3mV2W1hO4=', 1),
        ('usr_demo_400010', 'demo.camilo',    'Camilo Rojas',    'admin',    'pbkdf2-sha256$210000$ZqtE2hNwlp667/x6ACuNaQ==$nthyI+sjwEaltnp1/2UHxlTi65D1Z1ZSNrIRrsDYI3c=', 1);

    /* ================================================================
       6. RESERVAS (12)
       ================================================================ */
    INSERT dbo.Reservations
        (Id, Name, Phone, TableId, ReservationDate, ReservationTime, People, Status, CreatedBy)
    VALUES
        ('res_demo_300001', 'Mariana Lopez',   '3101112233',  1, @hoy,    CAST('12:30' AS time(0)), 2, 'confirmada', 'demo.laura'),
        ('res_demo_300002', 'Julian Perez',    '3102223344',  2, @hoy,    CAST('13:00' AS time(0)), 2, 'confirmada', 'demo.jorge'),
        ('res_demo_300003', 'Sandra Ruiz',     '3103334455',  3, @hoy,    CAST('19:30' AS time(0)), 2, 'confirmada', 'demo.valentina'),
        ('res_demo_300004', 'Diego Castro',    '3104445566',  4, @hoy,    CAST('20:00' AS time(0)), 2, 'atendida',   'demo.sofia'),
        ('res_demo_300005', 'Paula Vargas',    '3105556677',  5, @manana, CAST('12:00' AS time(0)), 4, 'confirmada', 'demo.laura'),
        ('res_demo_300006', 'Andres Gomez',    '3106667788',  6, @manana, CAST('19:00' AS time(0)), 4, 'confirmada', 'demo.jorge'),
        ('res_demo_300007', 'Natalia Rios',    '3107778899',  7, @manana, CAST('20:30' AS time(0)), 4, 'confirmada', 'demo.valentina'),
        ('res_demo_300008', 'Felipe Mora',     '3108889900',  8, @manana, CAST('21:00' AS time(0)), 4, 'confirmada', 'demo.sofia'),
        ('res_demo_300009', 'Camila Restrepo', '3109990011',  9, @mas2,   CAST('11:00' AS time(0)), 2, 'confirmada', 'demo.laura'),
        ('res_demo_300010', 'Oscar Leon',      '3111110022', 10, @mas2,   CAST('18:00' AS time(0)), 2, 'cancelada',  'demo.jorge'),
        ('res_demo_300011', 'Liliana Peña',    '3112220033', 11, @mas3,   CAST('13:30' AS time(0)), 4, 'confirmada', 'demo.valentina'),
        ('res_demo_300012', 'Ricardo Silva',   '3113330044', 12, @mas3,   CAST('20:00' AS time(0)), 6, 'confirmada', 'demo.sofia');

    /* ================================================================
       7. PEDIDOS (14)
          4 activos  -> aparecen en Cocina / resumen
          10 listos  -> habilitan despacho en la vista Despachos
       ================================================================ */
    INSERT dbo.Orders (Id, TableId, Notes, Status, CreatedBy)
    VALUES
        ('ord_demo_100001',  1, '',                            'pendiente',   'demo.laura'),
        ('ord_demo_100002',  2, 'Sin picante',                 'pendiente',   'demo.jorge'),
        ('ord_demo_100003',  5, 'Mesa de cumpleaños',          'preparacion', 'demo.valentina'),
        ('ord_demo_100004',  6, '',                            'pendiente',   'demo.sofia'),
        ('ord_demo_100005',  3, 'Para llevar',                 'listo',       'demo.laura'),
        ('ord_demo_100006',  4, 'Sin cebolla',                 'listo',       'demo.jorge'),
        ('ord_demo_100007',  7, 'Mesa junto a la ventana',     'listo',       'demo.valentina'),
        ('ord_demo_100008',  8, '',                            'listo',       'demo.sofia'),
        ('ord_demo_100009',  9, 'Cumpleaños, traer postre',    'listo',       'demo.laura'),
        ('ord_demo_100010', 10, '',                            'listo',       'demo.jorge'),
        ('ord_demo_100011', 11, 'Cliente frecuente',           'listo',       'demo.valentina'),
        ('ord_demo_100012', 12, 'Grupo grande',                'listo',       'demo.sofia'),
        ('ord_demo_100013',  1, 'Segundo pedido de la mesa',   'listo',       'demo.laura'),
        ('ord_demo_100014',  2, 'Postres',                     'listo',       'demo.jorge');

    /* ================================================================
       8. LINEAS DE PEDIDO (31)
       LineIndex empieza en 0 dentro de cada pedido.
       ================================================================ */
    INSERT dbo.OrderItems (OrderId, LineIndex, DishId, Quantity, Status)
    VALUES
        ('ord_demo_100001', 0, 'p1',  2, 'pendiente'),  ('ord_demo_100001', 1, 'p6',  2, 'pendiente'),
        ('ord_demo_100002', 0, 'p2',  1, 'pendiente'),  ('ord_demo_100002', 1, 'p7',  1, 'pendiente'),
        ('ord_demo_100003', 0, 'p3',  1, 'preparacion'),('ord_demo_100003', 1, 'p4',  1, 'preparacion'), ('ord_demo_100003', 2, 'p11', 2, 'listo'),
        ('ord_demo_100004', 0, 'p9',  2, 'pendiente'),  ('ord_demo_100004', 1, 'p12', 1, 'pendiente'),
        ('ord_demo_100005', 0, 'p2',  2, 'listo'),      ('ord_demo_100005', 1, 'p6',  1, 'listo'),
        ('ord_demo_100006', 0, 'p10', 1, 'listo'),      ('ord_demo_100006', 1, 'p5',  1, 'listo'),
        ('ord_demo_100007', 0, 'p1',  3, 'listo'),      ('ord_demo_100007', 1, 'p8',  2, 'listo'),
        ('ord_demo_100008', 0, 'p3',  1, 'listo'),      ('ord_demo_100008', 1, 'p9',  1, 'listo'),      ('ord_demo_100008', 2, 'p12', 1, 'listo'),
        ('ord_demo_100009', 0, 'p4',  2, 'listo'),      ('ord_demo_100009', 1, 'p11', 2, 'listo'),
        ('ord_demo_100010', 0, 'p5',  2, 'listo'),      ('ord_demo_100010', 1, 'p6',  3, 'listo'),
        ('ord_demo_100011', 0, 'p7',  1, 'listo'),      ('ord_demo_100011', 1, 'p8',  1, 'listo'),
        ('ord_demo_100012', 0, 'p2',  3, 'listo'),      ('ord_demo_100012', 1, 'p10', 2, 'listo'),      ('ord_demo_100012', 2, 'p12', 1, 'listo'),
        ('ord_demo_100013', 0, 'p1',  1, 'listo'),      ('ord_demo_100013', 1, 'p6',  1, 'listo'),
        ('ord_demo_100014', 0, 'p3',  2, 'listo'),      ('ord_demo_100014', 1, 'p5',  1, 'listo');

    /* ================================================================
       9. DESPACHOS (10), todos sobre pedidos que ya estan listos
       ================================================================ */
    INSERT dbo.Deliveries (Id, OrderId, TableId, Status)
    VALUES
        ('del_demo_200001', 'ord_demo_100005',  3, 'pendiente'),
        ('del_demo_200002', 'ord_demo_100006',  4, 'ruta'),
        ('del_demo_200003', 'ord_demo_100007',  7, 'entregado'),
        ('del_demo_200004', 'ord_demo_100008',  8, 'pendiente'),
        ('del_demo_200005', 'ord_demo_100009',  9, 'ruta'),
        ('del_demo_200006', 'ord_demo_100010', 10, 'pendiente'),
        ('del_demo_200007', 'ord_demo_100011', 11, 'entregado'),
        ('del_demo_200008', 'ord_demo_100012', 12, 'ruta'),
        ('del_demo_200009', 'ord_demo_100013',  1, 'pendiente'),
        ('del_demo_200010', 'ord_demo_100014',  2, 'entregado');

    /* ================================================================
       10. ESTADO DE MESAS 1-12 SEGUN PEDIDOS REALES
           ocupada si tiene algun pedido que no este 'listo'
       ================================================================ */
    UPDATE t
    SET t.Status = CASE
        WHEN EXISTS (SELECT 1 FROM dbo.Orders o WHERE o.TableId = t.Id AND o.Status <> 'listo')
            THEN 'ocupada'
        ELSE 'disponible'
    END
    FROM dbo.Tables t
    WHERE t.Id BETWEEN 1 AND 12;

    COMMIT TRANSACTION;

    PRINT 'Datos de prueba insertados correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH;

/* ================================================================
   11. VERIFICACION: conteo por tabla y detalle de lo insertado
   ================================================================ */
SELECT 'Users' AS Tabla, COUNT(*) AS Total FROM dbo.Users
UNION ALL SELECT 'Tables',       COUNT(*) FROM dbo.Tables
UNION ALL SELECT 'Dishes',       COUNT(*) FROM dbo.Dishes
UNION ALL SELECT 'Reservations', COUNT(*) FROM dbo.Reservations
UNION ALL SELECT 'Orders',       COUNT(*) FROM dbo.Orders
UNION ALL SELECT 'OrderItems',   COUNT(*) FROM dbo.OrderItems
UNION ALL SELECT 'Deliveries',   COUNT(*) FROM dbo.Deliveries;

SELECT 'RESERVAS' AS Tabla, Id, Name, TableId,
       CONVERT(char(10), ReservationDate, 23) AS Fecha,
       CONVERT(char(5),  ReservationTime, 108) AS Hora,
       People, Status
FROM dbo.Reservations WHERE Id LIKE 'res_demo%'
ORDER BY ReservationDate, ReservationTime;

SELECT 'PEDIDOS' AS Tabla, Id, TableId, Notes, Status
FROM dbo.Orders WHERE Id LIKE 'ord_demo%'
ORDER BY Id;

SELECT 'DESPACHOS' AS Tabla, Id, OrderId, TableId, Status
FROM dbo.Deliveries WHERE Id LIKE 'del_demo%'
ORDER BY Id;

SELECT 'USUARIOS' AS Tabla, Username, DisplayName, Role
FROM dbo.Users WHERE Username LIKE 'demo.%'
ORDER BY Username;

SELECT 'MESAS' AS Tabla, Id, Name, Seats, Status
FROM dbo.Tables ORDER BY Id;
