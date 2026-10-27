using DesafioReembolso.Application;

var builder = WebApplication.CreateBuilder(args);

// Registro dos serviços das camadas Core e Application
builder.Services.AddDesafioReembolso();

// Controllers e endpoints
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configuração de CORS para permitir requisições do frontend local (Vite, React, etc.)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.MapControllers();

// Endpoint raiz e health check simples
app.MapGet("/", () => Results.Ok(new
{
    Nome = "DesafioReembolso API",
    Versao = "1.0.0",
    Status = "Operacional",
    Documentacao = "/openapi/v1.json",
    Endpoints = new[]
    {
        "POST /api/reembolsos/processar-lote",
        "POST /api/reembolsos/processar-csv",
        "POST /api/reembolsos/exportar-csv",
        "GET  /api/reembolsos/regras"
    }
}));

app.Run();

// Necessário para habilitar WebApplicationFactory nos testes de integração
public partial class Program { }
