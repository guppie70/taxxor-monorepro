using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace System.Web
{
    public static class Context
    {
        private static IHttpContextAccessor? _contextAccessor;

        public static HttpContext? Current => _contextAccessor?.HttpContext;

        internal static void Configure(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }
    }
}





public static partial class FrameworkServicesExtensions
{
    public static void AddHttpContextAccessor(this IServiceCollection services)
    {
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    }

    public static IApplicationBuilder UseStaticHttpContext(this IApplicationBuilder app)
    {
        var httpContextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
        System.Web.Context.Configure(httpContextAccessor);
        return app;
    }

}