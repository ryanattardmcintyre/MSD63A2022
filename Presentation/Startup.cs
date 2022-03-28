using Google.Cloud.Diagnostics.Common;
using Google.Cloud.Diagnostics.AspNetCore3;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.SecretManager.V1;
using Newtonsoft.Json;

namespace Presentation
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", @"C:\Users\attar\Downloads\msd63a2022-e7c2e38466d6.json");

            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //install 1) using Google.Cloud.Diagnostics.Common;
            //        2) using Google.Cloud.Diagnostics.AspNetCore3;

            string projectId = Configuration["project"];
            services.AddGoogleErrorReportingForAspNetCore(
                new Google.Cloud.Diagnostics.Common.ErrorReportingServiceOptions
            {
                // Replace ProjectId with your Google Cloud Project ID.
                ProjectId = projectId,
                // Replace Service with a name or identifier for the service.
                ServiceName = "ClassDemo",
                // Replace Version with a version for the service.
                Version = "1"
            });


            services.AddLogging(builder => builder.AddGoogle(new LoggingServiceOptions
            {
                ProjectId = projectId,
                // Replace Service with a name or identifier for the service.
                ServiceName = "ClassDemo",
                // Replace Version with a version for the service.
                Version = "1"
            }));

            services.AddControllersWithViews();


            SecretManagerServiceClient client = SecretManagerServiceClient.Create();
            SecretVersionName secretVersionName = new SecretVersionName(projectId, "MyKeys", "1");
            AccessSecretVersionResponse result = client.AccessSecretVersion(secretVersionName);
            String payload = result.Payload.Data.ToStringUtf8();
            dynamic myObj = JsonConvert.DeserializeObject(payload);
            string clientId = Convert.ToString(myObj["Authentication:Google:ClientId"]);
            string clientSecret = Convert.ToString(myObj["Authentication:Google:ClientSecret"]);

            // requires
            // using Microsoft.AspNetCore.Authentication.Cookies;
            // using Microsoft.AspNetCore.Authentication.Google;
            // NuGet package Microsoft.AspNetCore.Authentication.Google
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
                })
                .AddCookie()
                .AddGoogle(options =>
                {
                    options.ClientId = clientId;
                    options.ClientSecret = clientSecret;
                });

            services.AddRazorPages();


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
