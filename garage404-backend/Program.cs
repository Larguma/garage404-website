using FirebaseAdmin;
using garage404_backend.Models;
using garage404_backend.Services;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;


var client = new HttpClient();
var keys = client
    .GetStringAsync(
        "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com")
    .Result;
var originalKeys = new JsonWebKeySet(keys).GetSigningKeys();
var additionalkeys = client
    .GetStringAsync(
        "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com")
    .Result;
var morekeys = new JsonWebKeySet(additionalkeys).GetSigningKeys();
var totalkeys = originalKeys.Concat(morekeys);

DotNetEnv.Env.Load();
var projectId = "garage404-68a8f";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<BackendDatabaseSettings>(builder.Configuration.GetSection("BackendDatabase"));
builder.Services.AddOptions<GoogleCalendarSettings>().Bind(builder.Configuration.GetSection("GoogleCalendar"));

builder.Services.AddHttpClient<IDiscordWebhookService, DiscordWebhookService>();
builder.Services.AddSingleton<UsersService>();
builder.Services.AddSingleton<ServiceTypesService>();
builder.Services.AddSingleton<ReservationsService>();
builder.Services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddSingleton(FirebaseApp.Create(
    new AppOptions() { Credential = CredentialFactory.FromFile(@"garage404-68a8f.json", JsonCredentialParameters.ServiceAccountCredentialType) }));


builder.Services.Configure<JsonOptions>(options =>
{
  options.SerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
  options.IncludeErrorDetails = true;
  options.Authority = $"https://securetoken.google.com/{projectId}";
  options.TokenValidationParameters = new TokenValidationParameters
  {
    ValidateIssuer = true,
    ValidIssuer = $"https://securetoken.google.com/{projectId}",
    ValidateAudience = true,
    ValidAudience = projectId,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    IssuerSigningKeys = totalkeys
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
        var usersService = context.HttpContext.RequestServices
            .GetRequiredService<UsersService>();

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
builder.Services.AddAuthorization();

builder.Services.AddOpenApi(options =>
{
  options.AddDocumentTransformer((document, _, _) =>
  {
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes.Add("FirebasePassword", new OpenApiSecurityScheme
    {
      Type = SecuritySchemeType.OAuth2,
      Flows = new OpenApiOAuthFlows
      {
        Password = new OpenApiOAuthFlow
        {
          TokenUrl = new Uri("/v1/Auth", UriKind.Relative),
          Scopes = new Dictionary<string, string>()
        }
      }
    });
    return Task.CompletedTask;
  });

  options.AddOperationTransformer((operation, context, _) =>
  {
    if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
    {
      operation.Security ??= [];
      operation.Security.Add(new OpenApiSecurityRequirement
      {
        [new OpenApiSecuritySchemeReference("FirebasePassword", context.Document)] = []
      });
    }

    return Task.CompletedTask;
  });
});

var app = builder.Build();

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
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
