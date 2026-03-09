    using HomeHubApp.Pages.Naplan.Services;

    var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register our question loader as singleton (loads once at startup)
builder.Services.AddSingleton<IQuestionService, QuestionService>();

// Session support (stores user answers between pages)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.WebHost.UseUrls(
    "http://0.0.0.0:5000",
    "https://0.0.0.0:5001"
);

builder.WebHost.UseKestrel(options =>
{
    options.ListenAnyIP(5000);           // http
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.UseHttps();        // uses dev cert automatically in Development env
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/data", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Context.Response.Headers.Append("Content-Type", "text/plain");
            ctx.Context.Response.WriteAsync("Directory access is not allowed.");
        }
    }
});

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();
app.UseSession();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
