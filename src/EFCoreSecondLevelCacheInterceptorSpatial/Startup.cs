using EFCoreSecondLevelCacheInterceptor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EFCoreSecondLevelCacheInterceptorSpatial
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddEFSecondLevelCache(x =>
                x.UseMemoryCacheProvider());

            services
                .AddEFSecondLevelCache(x =>
                {
                    x.UseMemoryCacheProvider();
                })
                .AddDbContext<ApplicationDbContext>(options =>
                {
                    options
                        .AddInterceptors(new SecondLevelCacheInterceptor())
                        .UseSqlServer("Data Source = .\\SQLEXPRESS; Database = EFCoreSecondLevelCacheInterceptorSpatial; Integrated Security = True;", x =>
                        {
                            x.UseNetTopologySuite();
                        });
                });

            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}