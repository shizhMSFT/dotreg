using Amazon.S3;
using Dotreg.Api.Middleware;
using Dotreg.Core.Services;
using Dotreg.Storage.S3;

var builder = WebApplication.CreateBuilder(args);

// Configure S3
var s3Config = builder.Configuration.GetSection("S3").Get<S3Config>() ?? new S3Config();
builder.Services.AddSingleton(s3Config);

// Configure AWS S3 client
var s3ClientConfig = new AmazonS3Config
{
    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3Config.Region),
    ForcePathStyle = s3Config.UsePathStyle
};

if (!string.IsNullOrEmpty(s3Config.ServiceUrl))
{
    s3ClientConfig.ServiceURL = s3Config.ServiceUrl;
}

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    if (!string.IsNullOrEmpty(s3Config.AccessKeyId) && !string.IsNullOrEmpty(s3Config.SecretAccessKey))
    {
        return new AmazonS3Client(s3Config.AccessKeyId, s3Config.SecretAccessKey, s3ClientConfig);
    }
    
    return new AmazonS3Client(s3ClientConfig);
});

// Register storage service
builder.Services.AddSingleton<IStorageService, S3StorageProvider>();

// Register upload session manager
builder.Services.AddScoped<IUploadSessionManager, UploadSessionManager>();

// Register registry service
builder.Services.AddScoped<IRegistryService, RegistryService>();

// Add controllers
builder.Services.AddControllers();

// Add health checks
builder.Services.AddHealthChecks();

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<RepositoryNameValidationMiddleware>();
app.UseMiddleware<DigestValidationMiddleware>();

// Map controllers
app.MapControllers();

// Map health check
app.MapHealthChecks("/health");

app.Run();
