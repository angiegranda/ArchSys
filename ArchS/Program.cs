using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ArchS.Data.AppServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(); // sets SignalIR for client-server communication

builder.Services.AddSingleton<ProfileCreationService>();
builder.Services.AddSingleton<BackupService>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Loopback, 5124); 
});
var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var backupService = scope.ServiceProvider.GetRequiredService<BackupService>();
    backupService.StartUpdateWatcher();
    backupService.StartMountWatcher();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// <a href="https://www.flaticon.com/free-icons/upload" title="upload icons">Upload icons created by Google - Flaticon</a>
// <a href="https://www.flaticon.com/free-icons/upload" title="upload icons">Upload icons created by Google - Flaticon</a>