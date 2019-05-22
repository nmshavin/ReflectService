using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReflectService.Hubs;
using System;

namespace ReflectService
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(o => o.AddPolicy("AllowAll", builder => { builder.AllowAnyHeader().WithOrigins("null").AllowAnyMethod().AllowCredentials(); }));
            services.AddResponseCompression();
            services.AddSession();
            services.AddSignalR();
            services.AddControllers();
            services.AddRazorPages();
            services.AddMvcCore().AddNewtonsoftJson();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (HostEnvironmentEnvExtensions.IsDevelopment(env))
            {
                app.UseCors("AllowAll");
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseCors("AllowAll");
                app.UseExceptionHandler("/Home/Error");
            }
            
            app.UseResponseCompression();
            app.UseStaticFiles();
            app.UseSession();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<ReflectServiceHub>("/reflectServiceHub");
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
