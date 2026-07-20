using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using BM.Service.Core.Extentions;
namespace BM.Service
{
    public class Startup
    {
        /// <summary>
        /// startup
        /// </summary>
        /// <param name="configuration">Config</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        /// <summary>
        ///  register service 
        /// </summary>
        /// <param name="services">services</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddExtensionsService(Configuration);
        }

        /// <summary>
        /// configure
        /// </summary>
        /// <param name="app">app</param>
        /// <param name="env">env</param>
        /// <param name="serviceProvider">serviceProvider</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider service_provider)
        {
            DatabaseInitializer.Initialize(service_provider, Configuration);
            app.UseExtensionsConfigure(env, service_provider, Configuration);
        }
    }
}
