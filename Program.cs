using SkyNet;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.Use(async (ctx, next) => await AnimalHospital.Core.Gate.Run(ctx, next));
app.UseMiddleware<IHandler>();
app.UseStaticHttpCurrent();

app.Run();
