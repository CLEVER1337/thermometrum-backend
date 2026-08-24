using thermometrum_backend.Configuration;
using thermometrum_backend.Ingest;
using thermometrum_backend.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.Configure<ClickHouseOptions>(builder.Configuration.GetSection(ClickHouseOptions.SectionName));
builder.Services.AddHttpClient(ClickHouseConnectionSource.HttpClientName);
builder.Services.AddSingleton<ClickHouseConnectionSource>();
builder.Services.AddSingleton<ReadingQueue>();
builder.Services.AddHostedService<ClickHouseWriter>();

builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.AddSingleton<MqttIngestService>();
builder.Services.AddHostedService(services => services.GetRequiredService<MqttIngestService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
