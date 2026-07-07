using System;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using EventHub.Admin.Web.Components;
using IdentityModel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.AspNetCore.Authentication.OpenIdConnect;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Server.BasicTheme;
using Volo.Abp.AspNetCore.Components.Server.BasicTheme.Bundling;
using Volo.Abp.AspNetCore.Components.Web.Theming.Routing;
using Volo.Abp.AspNetCore.Components.WebAssembly.BasicTheme.Bundling;
using Volo.Abp.AspNetCore.Mvc.Client;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic.Bundling;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement.Blazor.Server;
using Volo.Abp.Http.Client.IdentityModel.Web;
using Volo.Abp.Identity.Blazor.Server;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement.Blazor.Server;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation;

namespace EventHub.Admin.Web
{
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(AbpAutoMapperModule),
        typeof(AbpSwashbuckleModule),
        typeof(AbpAspNetCoreSerilogModule),
        typeof(AbpAspNetCoreMvcClientModule),
        typeof(AbpAspNetCoreAuthenticationOpenIdConnectModule),
        typeof(AbpHttpClientIdentityModelWebModule),
        typeof(EventHubAdminHttpApiClientModule),
        typeof(AbpAccountApplicationContractsModule),
        typeof(AbpIdentityBlazorServerModule),
        typeof(AbpSettingManagementBlazorServerModule),
        typeof(AbpFeatureManagementBlazorServerModule),
        typeof(AbpAspNetCoreComponentsServerBasicThemeModule),
        typeof(AbpAspNetCoreComponentsWebAssemblyBasicThemeBundlingModule),
        typeof(AbpAspNetCoreMvcUiBasicThemeModule)
    )]
    public class EventHubBlazorModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            PreConfigure<AbpAspNetCoreComponentsWebOptions>(options =>
            {
                options.IsBlazorWebApp = true;
            });
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            var configuration = context.Services.GetConfiguration();

            context.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            ConfigureAuthentication(context, configuration);
            ConfigureBundles(hostingEnvironment);
            ConfigureBlazorise(context);
            ConfigureRouter(context);
            ConfigureMenu(context);
            ConfigureAutoMapper(context);
        }

        private void ConfigureBundles(IWebHostEnvironment hostingEnvironment)
        {
            Configure<AbpBundlingOptions>(options =>
            {
                // Blazor Web App
                options.Parameters.InteractiveAuto = true;

                // MVC UI
                options.StyleBundles.Configure(
                    BasicThemeBundles.Styles.Global,
                    bundle =>
                    {
                        bundle.AddFiles("/global-styles.css");
                    }
                );

                options.ScriptBundles.Configure(
                    BasicThemeBundles.Scripts.Global,
                    bundle =>
                    {
                        bundle.AddFiles("/global-scripts.js");
                    }
                );

                // Blazor UI
                options.StyleBundles.Configure(
                    BlazorBasicThemeBundles.Styles.Global,
                    bundle =>
                    {
                        bundle.AddFiles("/global-styles.css");
                    }
                );
            });
        }

        private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.ExpireTimeSpan = TimeSpan.FromDays(365);
                })
                .AddAbpOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = configuration["AuthServer:Authority"];
                    options.RequireHttpsMetadata = Convert.ToBoolean(configuration["AuthServer:RequireHttpsMetadata"]);
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.ClientId = configuration["AuthServer:ClientId"];
                    options.ClientSecret = configuration["AuthServer:ClientSecret"];
                    options.UsePkce = true;
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;

                    options.Scope.Add("role");
                    options.Scope.Add("email");
                    options.Scope.Add("phone");
                    options.Scope.Add("EventHubAdmin");
                });
        }

        private void ConfigureRouter(ServiceConfigurationContext context)
        {
            Configure<AbpRouterOptions>(options =>
            {
                options.AppAssembly = typeof(EventHubBlazorModule).Assembly;
                options.AdditionalAssemblies.Add(typeof(EventHubBlazorClientModule).Assembly);
            });
        }

        private void ConfigureMenu(ServiceConfigurationContext context)
        {
            Configure<AbpNavigationOptions>(options =>
            {
                options.MenuContributors.Add(new EventHub.Admin.Web.Menus.EventHubMenuContributor(context.Services.GetConfiguration()));
            });
        }

        private void ConfigureBlazorise(ServiceConfigurationContext context)
        {
            context.Services
                .AddBootstrap5Providers()
                .AddFontAwesomeIcons();
        }

        private void ConfigureAutoMapper(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options =>
            {
                // Register the client's AutoMapper profiles on the server too, so the
                // InteractiveAuto server-side render path has the same object maps.
                options.AddMaps<EventHubBlazorClientModule>();
            });

            context.Services.AddAutoMapperObjectMapper();
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var env = context.GetEnvironment();
            var app = context.GetApplicationBuilder();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAbpRequestLocalization();

            if (!env.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseCorrelationId();
            app.UseRouting();
            app.MapAbpStaticAssets();
            app.UseAbpSecurityHeaders();
            app.UseAuthentication();
            app.UseUnitOfWork();
            app.UseDynamicClaims();
            app.UseAntiforgery();
            app.UseAuthorization();
            app.UseAuditing();
            app.UseAbpSerilogEnrichers();

            app.UseConfiguredEndpoints(builder =>
            {
                builder.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode()
                    .AddInteractiveWebAssemblyRenderMode()
                    .AddAdditionalAssemblies(builder.ServiceProvider.GetRequiredService<IOptions<AbpRouterOptions>>().Value.AdditionalAssemblies.ToArray());
            });
        }
    }
}
