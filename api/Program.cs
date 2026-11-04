using System.Security.Claims;
using System.Text.Json;
using GestionReservas.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");
builder.Services.AddSingleton<Database>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "MesaComun.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("admin", policy => policy.RequireRole("admin"));
    options.AddPolicy("service", policy => policy.RequireRole("admin", "mesero"));
    options.AddPolicy("kitchen", policy => policy.RequireRole("admin", "cocina"));
    options.AddPolicy("dispatch", policy => policy.RequireRole("admin", "mesero", "despacho"));
});
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MesaComun.Api");
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (InvalidOperationException error) { context.Response.StatusCode = StatusCodes.Status400BadRequest; await context.Response.WriteAsJsonAsync(new { error = error.Message }); }
    catch (KeyNotFoundException error) { context.Response.StatusCode = StatusCodes.Status404NotFound; await context.Response.WriteAsJsonAsync(new { error = error.Message }); }
    catch (Exception error)
    {
        logger.LogError(error, "Error procesando {Path}", context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = app.Environment.IsDevelopment() ? error.Message : "Ocurrió un error en el servidor." });
    }
});
app.UseAuthentication();
app.UseAuthorization();

var dataRoot = Directory.GetParent(app.Environment.ContentRootPath)?.FullName
    ?? throw new InvalidOperationException("No se encontró la carpeta de la interfaz.");
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings.Clear();
contentTypes.Mappings[".html"] = "text/html; charset=utf-8";
contentTypes.Mappings[".css"] = "text/css; charset=utf-8";
contentTypes.Mappings[".js"] = "text/javascript; charset=utf-8";
contentTypes.Mappings[".svg"] = "image/svg+xml";
contentTypes.Mappings[".png"] = "image/png";
contentTypes.Mappings[".ico"] = "image/x-icon";
contentTypes.Mappings[".woff2"] = "font/woff2";
var fileProvider = new PhysicalFileProvider(dataRoot);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider, ContentTypeProvider = contentTypes, ServeUnknownFileTypes = false });

var database = app.Services.GetRequiredService<Database>();
await database.InitializeAsync();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "Mesa Común API", database = "GestionReservas" }));
app.MapPost("/api/auth/login", async (LoginInput input, HttpContext context, Database db) =>
{
    var user = await db.FindUserAsync(input.Username);
    if (user is null || !Database.VerifyPassword(input.Password, user.PasswordHash)) return Results.Unauthorized();
    var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Role, user.Role), new Claim("username", user.Username) };
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Ok(new UserSummary(user.Id, user.Username, user.Name, user.Role));
});
app.MapPost("/api/auth/logout", async (HttpContext context) => { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.NoContent(); });

var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new UserSummary(user.FindFirstValue(ClaimTypes.NameIdentifier)!, user.FindFirstValue("username")!, user.Identity?.Name ?? "", user.FindFirstValue(ClaimTypes.Role)!)));
api.MapGet("/bootstrap", async (ClaimsPrincipal user, Database db) => Results.Ok(await db.GetBootstrapAsync(user.IsInRole("admin"))));
api.MapPost("/reservations", async (ReservationInput input, ClaimsPrincipal user, Database db) => Results.Ok(await db.CreateReservationAsync(input, user.FindFirstValue("username")!))).RequireAuthorization("service");
api.MapPatch("/reservations/{id}/status", async (string id, StatusInput input, Database db) => { await db.UpdateReservationAsync(id, input.Status); return Results.NoContent(); }).RequireAuthorization("service");
api.MapPost("/orders", async (OrderInput input, ClaimsPrincipal user, Database db) => Results.Ok(new { id = await db.CreateOrderAsync(input, user.FindFirstValue("username")!) })).RequireAuthorization("service");
api.MapPatch("/orders/{orderId}/lines/{lineIndex:int}", async (string orderId, int lineIndex, StatusInput input, Database db) => { await db.UpdateOrderLineAsync(orderId, lineIndex, input.Status); return Results.NoContent(); }).RequireAuthorization("kitchen");
api.MapPost("/deliveries", async (DeliveryInput input, Database db) => Results.Ok(new { id = await db.CreateDeliveryAsync(input.OrderId) })).RequireAuthorization("dispatch");
api.MapPatch("/deliveries/{id}/status", async (string id, StatusInput input, Database db) => { await db.UpdateDeliveryAsync(id, input.Status); return Results.NoContent(); }).RequireAuthorization("dispatch");
api.MapPost("/users", async (NewUserInput input, Database db) => Results.Ok(await db.AddUserAsync(input))).RequireAuthorization("admin");
api.MapDelete("/users/{id}", async (string id, ClaimsPrincipal user, Database db) => { await db.RemoveUserAsync(id, user.FindFirstValue(ClaimTypes.NameIdentifier)!); return Results.NoContent(); }).RequireAuthorization("admin");
api.MapPost("/reset-demo", async (Database db) => { await db.ResetDemoAsync(); return Results.NoContent(); }).RequireAuthorization("admin");

app.Run();

public sealed record DeliveryInput(string OrderId);
