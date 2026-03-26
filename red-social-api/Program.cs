using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using RedSocialApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<FirebaseService>();
;

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile("Firebase/firebase-key.json")
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");

app.MapControllers();

app.Run();