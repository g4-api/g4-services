using G4.Converters;
using G4.Services.Domain.V4.Formatters;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers(i =>
        // Add a custom input formatter to handle plain text inputs.
        i.InputFormatters.Add(new PlainTextInputFormatter()))
    .AddJsonOptions(i =>
    {
        // Configure JSON serializer to format JSON with indentation for readability.
        i.JsonSerializerOptions.WriteIndented = false;

        // Ignore properties with null values during serialization to reduce payload size.
        i.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // Use camelCase naming for JSON properties to follow JavaScript conventions.
        i.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;

        // Enable case-insensitive property name matching during deserialization.
        i.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

        // Add a custom type converter for handling specific types during serialization/deserialization.
        i.JsonSerializerOptions.Converters.Add(new TypeConverter());

        // Add a custom exception converter to handle exception serialization.
        i.JsonSerializerOptions.Converters.Add(new ExceptionConverter());

        // Add a custom DateTime converter to handle ISO 8601 date/time format.
        i.JsonSerializerOptions.Converters.Add(new DateTimeIso8601Converter());

        // Add a custom method base converter to handle method base serialization.
        i.JsonSerializerOptions.Converters.Add(new MethodBaseConverter());
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();
app.Run();
