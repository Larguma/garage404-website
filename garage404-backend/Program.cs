using System.Threading.RateLimiting;
using garage404_backend.Interfaces;
using garage404_backend.Models;
using garage404_backend.Models.Authentication;
using garage404_backend.Services;
using garage404_backend.Services.Utils;
using garage404_backend.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

var projectId = builder.Configuration["Firebase:ProjectId"] ?? "garage404-68a8f";

var webRootPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
Directory.CreateDirectory(webRootPath);
Directory.CreateDirectory(Path.Combine(webRootPath, "images"));
builder.WebHost.UseWebRoot(webRootPath);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
  options.AddPolicy(
    "Frontend",
    policy =>
    {
      if (allowedOrigins.Length == 0)
      {
        return;
      }

      policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
  );
});

// The API runs behind a reverse proxy (OpenResty) and is not exposed directly (see compose.yml),
// so the forwarded client IP and scheme can be trusted. They are needed for rate limiting and HTTPS redirection.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
  options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
  options.KnownIPNetworks.Clear();
  options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
  options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
  options.AddPolicy(
    RateLimitPolicies.Auth,
    context =>
      RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10),
          Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:Auth:WindowMinutes", 1)),
        }
      )
  );
  options.AddPolicy(
    RateLimitPolicies.PublicWrite,
    context =>
      RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
          PermitLimit = builder.Configuration.GetValue("RateLimiting:PublicWrite:PermitLimit", 5),
          Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:PublicWrite:WindowMinutes", 10)),
        }
      )
  );
});

builder.Services.Configure<BackendDatabaseSettings>(builder.Configuration.GetSection("BackendDatabase"));
builder.Services.AddOptions<GoogleCalendarSettings>().Bind(builder.Configuration.GetSection("GoogleCalendar"));
builder.Services.AddOptions<EmailSettings>().Bind(builder.Configuration.GetSection("Email"));

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IDiscordWebhookService, DiscordWebhookService>();
builder.Services.AddSingleton<BikeImageStorageService>();
builder.Services.AddSingleton<BikesService>();
builder.Services.AddSingleton<BlogService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddSingleton<ReservationsService>();
builder.Services.AddSingleton<ServiceTypesService>();
builder.Services.AddSingleton<UsersService>();
builder.Services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();

builder.Services.AddEndpointsApiExplorer();

builder
  .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
    options.IncludeErrorDetails = builder.Environment.IsDevelopment();
    // The signing keys are discovered from the authority's OpenID configuration and refreshed automatically
    // when Google rotates them.
    options.Authority = $"https://securetoken.google.com/{projectId}";
    options.TokenValidationParameters = new TokenValidationParameters
    {
      ValidateIssuer = true,
      ValidIssuer = $"https://securetoken.google.com/{projectId}",
      ValidateAudience = true,
      ValidAudience = projectId,
      ValidateLifetime = true,
      ValidateIssuerSigningKey = true,
    };
    options.Events = new JwtBearerEvents
    {
      // After the token is validated, check if the user exists in the database. If not, create a new user.
      OnTokenValidated = async context =>
      {
        var firebaseToken = context.SecurityToken as JsonWebToken;
        var firebaseUid = firebaseToken?.Claims.FirstOrDefault(c => c.Type == "user_id")?.Value;
        if (!string.IsNullOrEmpty(firebaseUid))
        {
          var usersService = context.HttpContext.RequestServices.GetRequiredService<UsersService>();

          var user = await usersService.GetAsync(firebaseUid);

          if (user == null)
          {
            user = new User
            {
              FirebaseUserId = firebaseUid,
              Email = firebaseToken?.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
              Name = firebaseToken?.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? $"{firebaseUid}",
            };
            await usersService.CreateAsync(user);
          }
        }
      },
    };
  });
builder
  .Services.AddAuthorizationBuilder()
  .AddPolicy(
    AuthorizationPolicies.Admin,
    policy => policy.RequireAuthenticatedUser().AddRequirements(new AdminRequirement())
  );

builder.Services.AddOpenApi(options =>
{
  options.AddDocumentTransformer(
    (document, _, _) =>
    {
      document.Components ??= new OpenApiComponents();
      document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
      document.Components.SecuritySchemes.Add(
        "FirebasePassword",
        new OpenApiSecurityScheme
        {
          Type = SecuritySchemeType.OAuth2,
          Flows = new OpenApiOAuthFlows
          {
            Password = new OpenApiOAuthFlow
            {
              TokenUrl = new Uri("/v1/Auth", UriKind.Relative),
              Scopes = new Dictionary<string, string>(),
            },
          },
        }
      );
      return Task.CompletedTask;
    }
  );

  options.AddOperationTransformer(
    (operation, context, _) =>
    {
      var metadata = context.Description.ActionDescriptor.EndpointMetadata;
      if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
      {
        operation.Security ??= [];
        operation.Security.Add(
          new OpenApiSecurityRequirement
          {
            [new OpenApiSecuritySchemeReference("FirebasePassword", context.Document)] = [],
          }
        );
      }

      return Task.CompletedTask;
    }
  );
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(
  async (context, next) =>
  {
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
  }
);

if (app.Environment.IsDevelopment())
{
  app.MapOpenApi();
  app.UseSwaggerUI(options =>
  {
    options.DocumentTitle = "Garage404 API";
    options.SwaggerEndpoint("/openapi/v1.json", "Garage404 API V1");
  });
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
