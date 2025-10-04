using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ExParcial.Data;
using StackExchange.Redis;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

// Redis configuration from appsettings
var redisSection = builder.Configuration.GetSection("Redis");
var host = redisSection.GetValue<string>("Host");
var port = redisSection.GetValue<int>("Port");
var user = redisSection.GetValue<string>("User");
var password = redisSection.GetValue<string>("Password");

var redisConfig = new ConfigurationOptions
{
    AbortOnConnectFail = false
};
redisConfig.EndPoints.Add(host, port);
if (!string.IsNullOrEmpty(user)) redisConfig.User = user;
if (!string.IsNullOrEmpty(password)) redisConfig.Password = password;

// Connect and register IConnectionMultiplexer singleton
var muxer = ConnectionMultiplexer.Connect(redisConfig);
builder.Services.AddSingleton<IConnectionMultiplexer>(muxer);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    var roleName = "Coordinador";
    if (!await roleManager.RoleExistsAsync(roleName))
        await roleManager.CreateAsync(new IdentityRole(roleName));

    var coordEmail = builder.Configuration["SEED_COORDINADOR_EMAIL"] ?? "coordinador@uni.local";
    var coordPwd = builder.Configuration["SEED_COORDINADOR_PASSWORD"] ?? "ChangeMe123!";

    var coordUser = await userManager.FindByEmailAsync(coordEmail);
    if (coordUser == null)
    {
        coordUser = new IdentityUser { UserName = coordEmail, Email = coordEmail, EmailConfirmed = true };
        var res = await userManager.CreateAsync(coordUser, coordPwd);
        if (res.Succeeded)
            await userManager.AddToRoleAsync(coordUser, roleName);
    }
}

app.Run();
